using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;

namespace MobileModSystem
{
    public sealed class ModPackageImporter : MonoBehaviour
    {
        [Header("보안 제한")]
        [Min(1)] public int maxPackageSizeMb = 100;
        [Min(1)] public int maxExtractedSizeMb = 250;
        [Min(1)] public int maxEntryCount = 512;

        [SerializeField] private RuntimeModAssetImporter assetImporter;

        private void Awake()
        {
            if (assetImporter == null)
                assetImporter = GetComponent<RuntimeModAssetImporter>();

            if (assetImporter == null)
                assetImporter = gameObject.AddComponent<RuntimeModAssetImporter>();
        }

        public async Task<GameObject> ImportAsync(string packagePath, Transform spawnParent)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                throw new FileNotFoundException("모드 패키지를 찾을 수 없습니다.", packagePath);

            long maxPackageBytes = (long)maxPackageSizeMb * 1024L * 1024L;
            if (new FileInfo(packagePath).Length > maxPackageBytes)
                throw new InvalidDataException("모드 파일이 허용 용량을 초과했습니다.");

            string extractDirectory = Path.Combine(
                Application.temporaryCachePath,
                "ModImport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractDirectory);

            List<GameObject> createdObjects = new List<GameObject>();

            try
            {
                ExtractPackageSafely(packagePath, extractDirectory);

                string manifestPath = Path.Combine(extractDirectory, ModPackageConstants.ManifestFileName);
                if (!File.Exists(manifestPath))
                    throw new InvalidDataException("manifest.json이 없는 모드 파일입니다.");

                ModPackageManifest manifest = JsonUtility.FromJson<ModPackageManifest>(
                    File.ReadAllText(manifestPath));
                ValidateManifest(manifest);

                Dictionary<string, ModAssetRecord> assets = new Dictionary<string, ModAssetRecord>();
                foreach (ModAssetRecord asset in manifest.assets)
                {
                    if (asset == null || string.IsNullOrWhiteSpace(asset.assetId))
                        throw new InvalidDataException("잘못된 에셋 항목이 있습니다.");

                    assets[asset.assetId] = asset;
                }

                Dictionary<string, GameObject> nodes = new Dictionary<string, GameObject>();

                // 1단계: 모든 노드를 먼저 생성합니다.
                foreach (ModNodeRecord record in manifest.nodes)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.id))
                        throw new InvalidDataException("잘못된 노드 ID가 있습니다.");
                    if (nodes.ContainsKey(record.id))
                        throw new InvalidDataException("중복된 노드 ID가 있습니다: " + record.id);

                    GameObject node = new GameObject(string.IsNullOrWhiteSpace(record.name) ? "ModNode" : record.name);
                    node.SetActive(false);
                    node.AddComponent<ModNode>().SetPersistentId(record.id);
                    nodes.Add(record.id, node);
                    createdObjects.Add(node);
                }

                // 2단계: 부모와 Transform을 복원합니다.
                foreach (ModNodeRecord record in manifest.nodes)
                {
                    GameObject node = nodes[record.id];
                    Transform parent = spawnParent;

                    if (!string.IsNullOrWhiteSpace(record.parentId))
                    {
                        if (!nodes.TryGetValue(record.parentId, out GameObject parentObject))
                            throw new InvalidDataException("부모 노드를 찾을 수 없습니다: " + record.parentId);

                        parent = parentObject.transform;
                    }

                    node.transform.SetParent(parent, false);
                    node.transform.localPosition = record.localPosition;
                    node.transform.localRotation = record.localRotation;
                    node.transform.localScale = record.localScale;
                }

                GameObject rootObject = GetSingleRootObject(manifest, nodes);

                // 기존 모드를 편집 후 다시 내보낼 때 원래 modId를 유지하기 위한 런타임 메타데이터입니다.
                RuntimeModPackageIdentity identity =
                    rootObject.GetComponent<RuntimeModPackageIdentity>();
                if (identity == null)
                    identity = rootObject.AddComponent<RuntimeModPackageIdentity>();

                identity.Initialize(
                    manifest.modId,
                    manifest.displayName,
                    manifest.author);

                // v2: 사용자가 편집한 설정 텍스트를 루트에 복원합니다.
                if (!string.IsNullOrWhiteSpace(manifest.settingsAssetId))
                {
                    string settingsPath = ResolveAssetPath(
                        manifest.settingsAssetId,
                        ModAssetType.ConfigText,
                        assets,
                        extractDirectory);

                    if (new FileInfo(settingsPath).Length > RuntimeModTextConfig.MaxTextBytes)
                        throw new InvalidDataException("모드 설정 텍스트가 256KB 제한을 초과했습니다.");

                    RuntimeModTextConfig config = rootObject.AddComponent<RuntimeModTextConfig>();
                    config.LoadFromFile(settingsPath, true);
                }
                else
                {
                    // 구형 v1 모드도 불러올 수 있게 기본 설정 컴포넌트를 생성합니다.
                    RuntimeModTextConfig config = rootObject.AddComponent<RuntimeModTextConfig>();
                    config.EnsureDefault(manifest.displayName, manifest.author);
                }

                // 3단계: 패키지 안에 모델이 여러 개 있어도 최신 모델 하나만 생성합니다.
                // 기존 MobileModController가 마지막 RuntimeModelBinding을 최신 모델로 사용했기 때문에
                // manifest.nodes 순서에서 마지막 modelAssetId를 가진 노드를 선택합니다.
                string selectedModelNodeId = null;
                int modelRecordCount = 0;

                foreach (ModNodeRecord record in manifest.nodes)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.modelAssetId))
                        continue;

                    selectedModelNodeId = record.id;
                    modelRecordCount++;
                }

                if (modelRecordCount > 1)
                {
                    Debug.LogWarning(
                        $"모드 패키지에 3D 모델이 {modelRecordCount}개 있습니다. " +
                        "가장 마지막 모델 하나만 불러옵니다.",
                        this);
                }

                if (!string.IsNullOrWhiteSpace(selectedModelNodeId))
                {
                    foreach (ModNodeRecord record in manifest.nodes)
                    {
                        if (record == null || record.id != selectedModelNodeId)
                            continue;

                        string modelPath = ResolveAssetPath(
                            record.modelAssetId,
                            ModAssetType.ModelGlb,
                            assets,
                            extractDirectory);

                        await assetImporter.LoadGlbIntoNodeAsync(
                            modelPath,
                            nodes[record.id],
                            true);

                        break;
                    }
                }

                // 4단계: 실제로 불러온 모델의 Renderer에만 텍스처를 적용합니다.
                foreach (ModNodeRecord record in manifest.nodes)
                {
                    // 모델을 가진 노드 중 선택되지 않은 이전 모델은 텍스처도 복원하지 않습니다.
                    if (!string.IsNullOrWhiteSpace(record.modelAssetId) &&
                        record.id != selectedModelNodeId)
                    {
                        continue;
                    }

                    GameObject node = nodes[record.id];
                    if (record.textures == null)
                        continue;

                    foreach (ModTextureRecord textureRecord in record.textures)
                    {
                        string texturePath = ResolveAssetPath(
                            textureRecord.assetId,
                            ModAssetType.Texture,
                            assets,
                            extractDirectory);

                        Transform rendererTransform = ModPathUtility.FindRelativeTransform(
                            node.transform,
                            textureRecord.rendererPath);
                        Renderer renderer = rendererTransform != null
                            ? rendererTransform.GetComponent<Renderer>()
                            : null;

                        if (renderer == null)
                        {
                            Debug.LogWarning("텍스처 대상 Renderer를 찾지 못했습니다: " + textureRecord.rendererPath);
                            continue;
                        }

                        await assetImporter.ImportTextureAsync(
                            texturePath,
                            renderer,
                            node,
                            textureRecord.propertyName,
                            textureRecord.materialIndex,
                            true);
                    }
                }

                // 5단계: 노드별 여러 오디오를 복원합니다.
                foreach (ModNodeRecord record in manifest.nodes)
                {
                    List<ModAudioRecord> audioRecords = new List<ModAudioRecord>();

                    if (record.audios != null)
                    {
                        foreach (ModAudioRecord item in record.audios)
                        {
                            if (item != null)
                                audioRecords.Add(item);
                        }
                    }

                    // v1/v2 모드팩 호환
                    if (record.hasAudio && record.audio != null)
                        audioRecords.Add(record.audio);

                    foreach (ModAudioRecord audioRecord in audioRecords)
                    {
                        string audioPath = ResolveAssetPath(
                            audioRecord.assetId,
                            ModAssetType.Audio,
                            assets,
                            extractDirectory);

                        if (!assets.TryGetValue(audioRecord.assetId, out ModAssetRecord audioAsset))
                            throw new InvalidDataException("오디오 에셋 정보를 찾을 수 없습니다: " + audioRecord.assetId);

                        string originalAudioFileName = string.IsNullOrWhiteSpace(audioAsset.originalFileName)
                            ? Path.GetFileName(audioPath)
                            : Path.GetFileName(audioAsset.originalFileName);

                        AudioSource source = await assetImporter.ImportAudioSourceAsync(
                            audioPath,
                            nodes[record.id],
                            audioRecord.playOnAwake,
                            audioRecord.loop,
                            audioRecord.volume,
                            audioRecord.spatialBlend,
                            true,
                            originalAudioFileName);

                        source.minDistance = audioRecord.minDistance;
                        source.maxDistance = audioRecord.maxDistance;
                    }
                }

                // 자식부터가 아니라 기록된 activeSelf를 그대로 최종 적용합니다.
                foreach (ModNodeRecord record in manifest.nodes)
                    nodes[record.id].SetActive(record.activeSelf);

                return rootObject;
            }
            catch
            {
                foreach (GameObject created in createdObjects)
                {
                    if (created != null)
                        Destroy(created);
                }

                throw;
            }
            finally
            {
                if (Directory.Exists(extractDirectory))
                    Directory.Delete(extractDirectory, true);
            }
        }

        private void ExtractPackageSafely(string packagePath, string destinationRoot)
        {
            string normalizedRoot = Path.GetFullPath(destinationRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            long extractedBytes = 0;
            int entryCount = 0;
            long maxExtractedBytes = (long)maxExtractedSizeMb * 1024L * 1024L;

            using (FileStream stream = File.OpenRead(packagePath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    entryCount++;
                    if (entryCount > maxEntryCount)
                        throw new InvalidDataException("모드 압축 파일의 항목 수가 너무 많습니다.");

                    extractedBytes += entry.Length;
                    if (extractedBytes > maxExtractedBytes)
                        throw new InvalidDataException("모드 압축 해제 용량이 제한을 초과했습니다.");

                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, relative));

                    if (!destinationPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
                        throw new InvalidDataException("허용되지 않은 압축 경로가 포함되어 있습니다.");

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                        input.CopyTo(output);
                }
            }
        }

        private static void ValidateManifest(ModPackageManifest manifest)
        {
            if (manifest == null)
                throw new InvalidDataException("manifest.json을 해석할 수 없습니다.");
            if (!string.Equals(manifest.magic, ModPackageConstants.Magic, StringComparison.Ordinal))
                throw new InvalidDataException("지원하지 않는 모드 파일입니다.");
            if (manifest.formatVersion < ModPackageConstants.MinimumSupportedVersion ||
                manifest.formatVersion > ModPackageConstants.CurrentVersion)
                throw new InvalidDataException("지원하지 않는 모드 버전입니다: " + manifest.formatVersion);
            if (manifest.assets == null || manifest.nodes == null || manifest.nodes.Count == 0)
                throw new InvalidDataException("모드 데이터가 비어 있습니다.");
        }

        private static GameObject GetSingleRootObject(
            ModPackageManifest manifest,
            Dictionary<string, GameObject> nodes)
        {
            GameObject root = null;

            foreach (ModNodeRecord record in manifest.nodes)
            {
                if (!string.IsNullOrWhiteSpace(record.parentId))
                    continue;

                if (root != null)
                    throw new InvalidDataException("루트 노드가 여러 개인 모드 파일입니다.");

                root = nodes[record.id];
            }

            if (root == null)
                throw new InvalidDataException("루트 노드가 없는 모드 파일입니다.");

            return root;
        }

        private static string ResolveAssetPath(
            string assetId,
            ModAssetType expectedType,
            Dictionary<string, ModAssetRecord> assets,
            string extractDirectory)
        {
            if (string.IsNullOrWhiteSpace(assetId) || !assets.TryGetValue(assetId, out ModAssetRecord asset))
                throw new InvalidDataException("모드 에셋을 찾을 수 없습니다: " + assetId);
            if (asset.assetType != expectedType)
                throw new InvalidDataException("모드 에셋 종류가 일치하지 않습니다: " + assetId);

            string root = Path.GetFullPath(extractDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string relative = asset.relativePath.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(extractDirectory, relative));

            if (!fullPath.StartsWith(root, StringComparison.Ordinal) || !File.Exists(fullPath))
                throw new InvalidDataException("모드 에셋 경로가 잘못되었습니다: " + asset.relativePath);

            return fullPath;
        }
    }
}
