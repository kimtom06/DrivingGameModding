using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;

namespace MobileModSystem
{
    public sealed class ModPackageExporter : MonoBehaviour
    {
        public Task<string> ExportAsync(GameObject root, string modName, string author)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            ModNode rootNode = root.GetComponent<ModNode>();
            if (rootNode == null)
                rootNode = root.AddComponent<ModNode>();

            string safeName = ModPathUtility.MakeSafeFileName(modName, root.name);
            string stagingDirectory = Path.Combine(
                Application.temporaryCachePath,
                "ModExport_" + Guid.NewGuid().ToString("N"));
            string outputPath = Path.Combine(
                Application.temporaryCachePath,
                safeName + ModPackageConstants.Extension);

            Directory.CreateDirectory(stagingDirectory);

            try
            {
                RuntimeModPackageIdentity identity =
                    root.GetComponent<RuntimeModPackageIdentity>();

                string preservedModId = identity != null &&
                                        !string.IsNullOrWhiteSpace(identity.ModId)
                    ? identity.ModId
                    : Guid.NewGuid().ToString("N");

                ModPackageManifest manifest = new ModPackageManifest
                {
                    modId = preservedModId,
                    displayName = safeName,
                    author = author ?? string.Empty,
                    createdUtc = DateTime.UtcNow.ToString("O")
                };

                Dictionary<string, string> sourcePathToAssetId =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                SerializeNodeRecursive(
                    root.transform,
                    string.Empty,
                    manifest,
                    stagingDirectory,
                    sourcePathToAssetId);

                // v2: 루트에 있는 사용자 편집용 설정 텍스트를 항상 모드팩에 포함합니다.
                RuntimeModTextConfig textConfig = root.GetComponent<RuntimeModTextConfig>();
                if (textConfig == null)
                    textConfig = root.AddComponent<RuntimeModTextConfig>();

                textConfig.EnsureDefault(safeName, author);
                manifest.settingsAssetId = RegisterTextAsset(
                    textConfig.TextContent,
                    RuntimeModTextConfig.DefaultFileName,
                    manifest,
                    stagingDirectory);

                string manifestPath = Path.Combine(stagingDirectory, ModPackageConstants.ManifestFileName);
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));

                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                using (FileStream fileStream = new FileStream(outputPath, FileMode.CreateNew))
                using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    AddDirectoryToZip(archive, stagingDirectory, stagingDirectory);
                }

                if (identity == null)
                    identity = root.AddComponent<RuntimeModPackageIdentity>();

                identity.Initialize(
                    manifest.modId,
                    manifest.displayName,
                    manifest.author);

                return Task.FromResult(outputPath);
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                    Directory.Delete(stagingDirectory, true);
            }
        }

        private static void SerializeNodeRecursive(
            Transform current,
            string parentId,
            ModPackageManifest manifest,
            string stagingDirectory,
            Dictionary<string, string> sourcePathToAssetId)
        {
            ModNode modNode = current.GetComponent<ModNode>();
            if (modNode == null)
                return;

            ModNodeRecord record = new ModNodeRecord
            {
                id = modNode.PersistentId,
                parentId = parentId,
                name = current.name,
                activeSelf = current.gameObject.activeSelf,
                localPosition = current.localPosition,
                localRotation = current.localRotation,
                localScale = current.localScale
            };

            RuntimeModelBinding model = current.GetComponent<RuntimeModelBinding>();
            if (model != null && !string.IsNullOrWhiteSpace(model.sourceFilePath))
            {
                record.modelAssetId = RegisterAsset(
                    model.sourceFilePath,
                    ModAssetType.ModelGlb,
                    manifest,
                    stagingDirectory,
                    sourcePathToAssetId);
            }

            RuntimeTextureBinding[] textureBindings = current.GetComponents<RuntimeTextureBinding>();
            foreach (RuntimeTextureBinding binding in textureBindings)
            {
                if (binding == null || binding.targetRenderer == null ||
                    string.IsNullOrWhiteSpace(binding.sourceFilePath))
                    continue;

                string rendererPath = ModPathUtility.GetRelativeTransformPath(
                    current,
                    binding.targetRenderer.transform);

                if (binding.targetRenderer.transform != current && string.IsNullOrEmpty(rendererPath))
                {
                    Debug.LogWarning("텍스처 대상 Renderer가 모드 노드 밖에 있어 제외합니다: " + binding.targetRenderer.name);
                    continue;
                }

                record.textures.Add(new ModTextureRecord
                {
                    assetId = RegisterAsset(
                        binding.sourceFilePath,
                        ModAssetType.Texture,
                        manifest,
                        stagingDirectory,
                        sourcePathToAssetId),
                    rendererPath = rendererPath,
                    materialIndex = binding.materialIndex,
                    propertyName = binding.propertyName
                });
            }

            RuntimeAudioBinding[] audioBindings = current.GetComponents<RuntimeAudioBinding>();
            foreach (RuntimeAudioBinding audioBinding in audioBindings)
            {
                if (audioBinding == null || audioBinding.targetAudioSource == null ||
                    string.IsNullOrWhiteSpace(audioBinding.sourceFilePath))
                    continue;

                AudioSource source = audioBinding.targetAudioSource;
                record.audios.Add(new ModAudioRecord
                {
                    assetId = RegisterAsset(
                        audioBinding.sourceFilePath,
                        ModAssetType.Audio,
                        manifest,
                        stagingDirectory,
                        sourcePathToAssetId,
                        audioBinding.originalFileName),
                    playOnAwake = source.playOnAwake,
                    loop = source.loop,
                    volume = source.volume,
                    spatialBlend = source.spatialBlend,
                    minDistance = source.minDistance,
                    maxDistance = source.maxDistance
                });
            }

            manifest.nodes.Add(record);

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                // GLB 내부에서 자동 생성된 오브젝트는 ModNode가 없으므로 중복 저장하지 않습니다.
                if (child.GetComponent<ModNode>() != null)
                {
                    SerializeNodeRecursive(
                        child,
                        record.id,
                        manifest,
                        stagingDirectory,
                        sourcePathToAssetId);
                }
            }
        }

        private static string RegisterAsset(
            string sourcePath,
            ModAssetType assetType,
            ModPackageManifest manifest,
            string stagingDirectory,
            Dictionary<string, string> sourcePathToAssetId,
            string originalFileNameOverride = null)
        {
            string normalizedPath = Path.GetFullPath(sourcePath);
            if (!File.Exists(normalizedPath))
                throw new FileNotFoundException("내보낼 모드 에셋이 없습니다.", normalizedPath);

            if (sourcePathToAssetId.TryGetValue(normalizedPath, out string existingId))
                return existingId;

            string assetId = Guid.NewGuid().ToString("N");
            string extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
            string category = assetType.ToString();
            string relativePath = Path.Combine("assets", category, assetId + extension)
                .Replace('\\', '/');
            string destination = Path.Combine(
                stagingDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(normalizedPath, destination, true);

            manifest.assets.Add(new ModAssetRecord
            {
                assetId = assetId,
                assetType = assetType,
                relativePath = relativePath,
                originalFileName = string.IsNullOrWhiteSpace(originalFileNameOverride)
                    ? Path.GetFileName(normalizedPath)
                    : Path.GetFileName(originalFileNameOverride)
            });

            sourcePathToAssetId.Add(normalizedPath, assetId);
            return assetId;
        }


        private static string RegisterTextAsset(
            string text,
            string originalFileName,
            ModPackageManifest manifest,
            string stagingDirectory)
        {
            string assetId = Guid.NewGuid().ToString("N");
            string relativePath = Path.Combine(
                    "assets",
                    ModAssetType.ConfigText.ToString(),
                    assetId + ".txt")
                .Replace('\\', '/');
            string destination = Path.Combine(
                stagingDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllText(destination, text ?? string.Empty, new UTF8Encoding(false));

            manifest.assets.Add(new ModAssetRecord
            {
                assetId = assetId,
                assetType = ModAssetType.ConfigText,
                relativePath = relativePath,
                originalFileName = string.IsNullOrWhiteSpace(originalFileName)
                    ? RuntimeModTextConfig.DefaultFileName
                    : Path.GetFileName(originalFileName)
            });

            return assetId;
        }

        private static void AddDirectoryToZip(
            ZipArchive archive,
            string rootDirectory,
            string currentDirectory)
        {
            foreach (string file in Directory.GetFiles(currentDirectory))
            {
                string relative = file.Substring(rootDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');

                ZipArchiveEntry entry = archive.CreateEntry(relative, System.IO.Compression.CompressionLevel.Optimal);
                using (Stream input = File.OpenRead(file))
                using (Stream output = entry.Open())
                    input.CopyTo(output);
            }

            foreach (string directory in Directory.GetDirectories(currentDirectory))
                AddDirectoryToZip(archive, rootDirectory, directory);
        }
    }
}
