using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Networking;

namespace MobileModSystem
{
    public sealed class RuntimeModAssetImporter : MonoBehaviour
    {
        [Header("Imported GLB Materials")]
        [SerializeField]
        private bool convertImportedMaterialsToStandard = true;

        [Tooltip("Assign Unity's Built-in Render Pipeline Standard shader here so it is retained in builds.")]
        [SerializeField]
        private Shader standardShader;

        [SerializeField]
        private bool repackMetallicRoughnessTexture = true;

        [SerializeField]
        private bool repackOcclusionTexture = true;

        /// <summary>
        /// 공개 GLB Import API도 항상 parent 아래 모델을 하나만 유지합니다.
        /// 기존 모델이 있으면 자동으로 교체됩니다.
        /// </summary>
        public async Task<GameObject> ImportGlbAsync(
            string pickedFilePath,
            Transform parent,
            string objectName = null,
            bool copyIntoWorkspace = true)
        {
            GameObject result = await ImportOrReplaceSingleGlbAsync(
                pickedFilePath,
                parent,
                null,
                copyIntoWorkspace);

            if (!string.IsNullOrWhiteSpace(objectName) && result != null)
                result.name = objectName;

            return result;
        }

        private async Task<GameObject> CreateNewGlbNodeAsync(
            string pickedFilePath,
            Transform parent,
            string objectName = null,
            bool copyIntoWorkspace = true)
        {
            ValidateExtension(pickedFilePath, ".glb");

            string storedPath = copyIntoWorkspace
                ? ModPathUtility.CopyIntoWorkspace(pickedFilePath, "Models")
                : pickedFilePath;

            string finalName = string.IsNullOrWhiteSpace(objectName)
                ? Path.GetFileNameWithoutExtension(pickedFilePath)
                : objectName;

            GameObject root = new GameObject(finalName);
            root.transform.SetParent(parent, false);
            root.AddComponent<ModNode>();

            try
            {
                await LoadGlbIntoNodeAsync(storedPath, root, false);
                return root;
            }
            catch
            {
                Destroy(root);
                throw;
            }
        }

        /// <summary>
        /// parent 아래의 3D 모델을 항상 하나만 유지합니다.
        /// 기존 모델이 있으면 같은 모델 노드의 __Model 내용만 새 GLB로 교체하여
        /// AudioStorage 같은 ModNode 자식은 유지합니다.
        /// 여러 RuntimeModelBinding이 이미 존재하면 preferredExistingRoot 또는 마지막 모델만 남깁니다.
        /// </summary>
        public async Task<GameObject> ImportOrReplaceSingleGlbAsync(
            string pickedFilePath,
            Transform parent,
            GameObject preferredExistingRoot = null,
            bool copyIntoWorkspace = true)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            ValidateExtension(pickedFilePath, ".glb");

            RuntimeModelBinding[] existingModels =
                parent.GetComponentsInChildren<RuntimeModelBinding>(true);

            GameObject targetRoot = null;

            if (preferredExistingRoot != null &&
                IsSameOrChildOf(preferredExistingRoot.transform, parent) &&
                preferredExistingRoot.GetComponent<RuntimeModelBinding>() != null)
            {
                targetRoot = preferredExistingRoot;
            }
            else if (existingModels.Length > 0)
            {
                // 기존 동작과 동일하게 가장 마지막 모델을 최신 모델로 취급합니다.
                targetRoot = existingModels[existingModels.Length - 1].gameObject;
            }

            if (targetRoot == null)
            {
                return await CreateNewGlbNodeAsync(
                    pickedFilePath,
                    parent,
                    null,
                    copyIntoWorkspace);
            }

            // 선택된 최신 모델 외의 모든 모델을 제거합니다.
            foreach (RuntimeModelBinding binding in existingModels)
            {
                if (binding == null || binding.gameObject == targetRoot)
                    continue;

                GameObject duplicateRoot = binding.gameObject;

                // buildRoot 자체이거나 targetRoot의 조상이라면 오브젝트 전체를 지우면 안 됩니다.
                if (duplicateRoot.transform == parent ||
                    targetRoot.transform.IsChildOf(duplicateRoot.transform))
                {
                    RemoveModelFromNode(duplicateRoot, true);
                }
                else
                {
                    duplicateRoot.SetActive(false);
                    Destroy(duplicateRoot);
                }
            }

            // 기존 모델 노드는 유지하고 내부 GLB만 새 모델로 교체합니다.
            await LoadGlbIntoNodeAsync(
                pickedFilePath,
                targetRoot,
                copyIntoWorkspace);

            string newName = Path.GetFileNameWithoutExtension(pickedFilePath);
            if (!string.IsNullOrWhiteSpace(newName))
                targetRoot.name = newName;

            return targetRoot;
        }

        public async Task LoadGlbIntoNodeAsync(
            string sourcePath,
            GameObject node,
            bool copyIntoWorkspace = true)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            ValidateExtension(sourcePath, ".glb");

            string storedPath = copyIntoWorkspace
                ? ModPathUtility.CopyIntoWorkspace(sourcePath, "Models")
                : sourcePath;

            byte[] bytes = File.ReadAllBytes(storedPath);
            GltfImport gltf = new GltfImport();

            bool loaded = await gltf.LoadGltfBinary(bytes, ModPathUtility.ToFileUri(storedPath));
            if (!loaded)
                throw new InvalidDataException("glTFast가 GLB 모델을 읽지 못했습니다: " + storedPath);

            // 새 모델을 먼저 임시 컨테이너에 완전히 생성한 뒤 기존 모델과 교체합니다.
            // 새 GLB 로딩/생성 실패 시 기존 모델이 그대로 남도록 하기 위함입니다.
            GameObject modelContainer = new GameObject("__Model_New");
            modelContainer.transform.SetParent(node.transform, false);
            modelContainer.SetActive(false);

            bool instantiated = await gltf.InstantiateMainSceneAsync(modelContainer.transform);
            if (!instantiated)
            {
                Destroy(modelContainer);
                throw new InvalidDataException("GLB 모델의 씬 생성에 실패했습니다: " + storedPath);
            }

            if (convertImportedMaterialsToStandard)
            {
                Shader resolvedStandardShader = standardShader != null
                    ? standardShader
                    : Shader.Find("Standard");

                if (resolvedStandardShader == null)
                {
                    Destroy(modelContainer);
                    throw new InvalidOperationException(
                        "Unity Standard shader was not found. Assign it to RuntimeModAssetImporter.standardShader " +
                        "or add Standard to Graphics > Always Included Shaders.");
                }

                int convertedMaterialCount = RuntimeStandardMaterialConverter.ConvertHierarchy(
                    modelContainer,
                    resolvedStandardShader,
                    repackMetallicRoughnessTexture,
                    repackOcclusionTexture);

                Debug.Log(
                    $"Converted {convertedMaterialCount} imported glTF materials to Standard on {node.name}.",
                    node);
            }

            // 여기까지 성공했을 때만 기존 GLB 컨테이너와 오래된 텍스처 바인딩을 제거합니다.
            RemoveGeneratedModelContainers(node, modelContainer);
            RemoveTextureBindings(node);

            modelContainer.name = "__Model";
            modelContainer.SetActive(true);

            RuntimeModelBinding binding = node.GetComponent<RuntimeModelBinding>();
            if (binding == null)
                binding = node.AddComponent<RuntimeModelBinding>();

            binding.sourceFilePath = storedPath;
        }

        /// <summary>
        /// 지정한 ModNode의 실제 GLB 내용만 제거합니다.
        /// removeBinding=true이면 RuntimeModelBinding도 제거합니다.
        /// AudioStorage 같은 다른 ModNode 자식은 건드리지 않습니다.
        /// </summary>
        public void RemoveModelFromNode(GameObject node, bool removeBinding = true)
        {
            if (node == null)
                return;

            RemoveGeneratedModelContainers(node, null);
            RemoveTextureBindings(node);

            if (removeBinding)
            {
                RuntimeModelBinding binding = node.GetComponent<RuntimeModelBinding>();
                if (binding != null)
                    Destroy(binding);
            }
        }

        private static bool IsSameOrChildOf(Transform candidate, Transform parent)
        {
            if (candidate == null || parent == null)
                return false;

            return candidate == parent || candidate.IsChildOf(parent);
        }

        private static void RemoveGeneratedModelContainers(
            GameObject node,
            GameObject keepContainer)
        {
            if (node == null)
                return;

            for (int i = node.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = node.transform.GetChild(i);
                if (child == null || child.gameObject == keepContainer)
                    continue;

                // RuntimeModAssetImporter가 생성한 GLB 전용 컨테이너만 제거합니다.
                if (child.name == "__Model" || child.name.StartsWith("__Model_", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }

        private static void RemoveTextureBindings(GameObject node)
        {
            RuntimeTextureBinding[] textureBindings =
                node.GetComponents<RuntimeTextureBinding>();

            foreach (RuntimeTextureBinding textureBinding in textureBindings)
            {
                if (textureBinding != null)
                    Destroy(textureBinding);
            }
        }

        public async Task<Texture2D> ImportTextureAsync(
            string sourcePath,
            Renderer targetRenderer,
            GameObject ownerNode,
            string propertyName = "_BaseMap",
            int materialIndex = 0,
            bool copyIntoWorkspace = true)
        {
            if (targetRenderer == null)
                throw new ArgumentNullException(nameof(targetRenderer));
            if (ownerNode == null)
                throw new ArgumentNullException(nameof(ownerNode));

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                throw new NotSupportedException("텍스처는 PNG/JPG/JPEG만 지원합니다.");

            string storedPath = copyIntoWorkspace
                ? ModPathUtility.CopyIntoWorkspace(sourcePath, "Textures")
                : sourcePath;

            byte[] bytes = File.ReadAllBytes(storedPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            texture.name = Path.GetFileNameWithoutExtension(storedPath);

            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                Destroy(texture);
                throw new InvalidDataException("텍스처를 읽지 못했습니다: " + storedPath);
            }

            ApplyTexture(targetRenderer, texture, propertyName, materialIndex);

            RuntimeTextureBinding binding = ownerNode.AddComponent<RuntimeTextureBinding>();
            binding.sourceFilePath = storedPath;
            binding.targetRenderer = targetRenderer;
            binding.materialIndex = materialIndex;
            binding.propertyName = propertyName;

            await Task.Yield();
            return texture;
        }

        /// <summary>
        /// ownerNode에 새로운 AudioSource와 RuntimeAudioBinding을 추가합니다.
        /// 같은 AudioStorage 오브젝트에 여러 번 호출하면 AudioSource가 누적됩니다.
        /// </summary>
        public async Task<AudioSource> ImportAudioSourceAsync(
            string sourcePath,
            GameObject ownerNode,
            bool playOnAwake = false,
            bool loop = false,
            float volume = 1f,
            float spatialBlend = 0f,
            bool copyIntoWorkspace = true,
            string originalFileName = null)
        {
            if (ownerNode == null)
                throw new ArgumentNullException(nameof(ownerNode));

            AudioType audioType = GetAudioType(sourcePath);
            string storedPath = copyIntoWorkspace
                ? ModPathUtility.CopyIntoWorkspace(sourcePath, "Audio")
                : sourcePath;

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                       ModPathUtility.ToFileUri(storedPath).AbsoluteUri, audioType))
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidDataException("오디오를 읽지 못했습니다: " + request.error);

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                    throw new InvalidDataException("AudioClip 생성에 실패했습니다: " + storedPath);

                // 모드 패키지 내부에서는 에셋 파일명이 GUID로 바뀌므로,
                // 실제 AudioClip.name은 패키지 manifest에 저장된 원래 파일명을 사용합니다.
                string resolvedOriginalFileName = string.IsNullOrWhiteSpace(originalFileName)
                    ? Path.GetFileName(sourcePath)
                    : Path.GetFileName(originalFileName);

                if (string.IsNullOrWhiteSpace(resolvedOriginalFileName))
                    resolvedOriginalFileName = Path.GetFileName(storedPath);

                clip.name = Path.GetFileNameWithoutExtension(resolvedOriginalFileName);

                // 항상 새 AudioSource를 생성하여 기존 오디오를 덮어쓰지 않습니다.
                AudioSource audioSource = ownerNode.AddComponent<AudioSource>();
                audioSource.clip = clip;
                audioSource.playOnAwake = playOnAwake;
                audioSource.loop = loop;
                audioSource.volume = Mathf.Clamp01(volume);
                audioSource.spatialBlend = Mathf.Clamp01(spatialBlend);

                // RuntimeAudioBinding도 AudioSource마다 하나씩 생성합니다.
                RuntimeAudioBinding binding = ownerNode.AddComponent<RuntimeAudioBinding>();
                binding.sourceFilePath = storedPath;
                binding.originalFileName = resolvedOriginalFileName;
                binding.targetAudioSource = audioSource;

                return audioSource;
            }
        }

        // 기존 코드와의 호환을 위한 래퍼입니다.
        public async Task<AudioClip> ImportAudioAsync(
            string sourcePath,
            GameObject ownerNode,
            bool playOnAwake = false,
            bool loop = false,
            float volume = 1f,
            float spatialBlend = 0f,
            bool copyIntoWorkspace = true,
            string originalFileName = null)
        {
            AudioSource source = await ImportAudioSourceAsync(
                sourcePath,
                ownerNode,
                playOnAwake,
                loop,
                volume,
                spatialBlend,
                copyIntoWorkspace,
                originalFileName);

            return source.clip;
        }

        public static void ApplyTexture(
            Renderer renderer,
            Texture texture,
            string propertyName,
            int materialIndex)
        {
            Material[] materials = renderer.materials;
            if (materialIndex < 0 || materialIndex >= materials.Length)
                throw new ArgumentOutOfRangeException(nameof(materialIndex));

            Material material = materials[materialIndex];
            string actualProperty = propertyName;

            if (string.IsNullOrWhiteSpace(actualProperty) || !material.HasProperty(actualProperty))
            {
                if (material.HasProperty("_BaseMap"))
                    actualProperty = "_BaseMap";
                else if (material.HasProperty("_MainTex"))
                    actualProperty = "_MainTex";
                else
                    throw new InvalidOperationException("선택한 머티리얼에 적용 가능한 텍스처 프로퍼티가 없습니다.");
            }

            material.SetTexture(actualProperty, texture);
            renderer.materials = materials;
        }

        private static AudioType GetAudioType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".wav": return AudioType.WAV;
                case ".ogg": return AudioType.OGGVORBIS;
                case ".mp3": return AudioType.MPEG;
                case ".aif":
                case ".aiff": return AudioType.AIFF;
                default:
                    throw new NotSupportedException("오디오는 WAV, OGG, MP3, AIFF만 지원합니다.");
            }
        }

        private static void ValidateExtension(string path, string expectedExtension)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("파일을 찾을 수 없습니다.", path);

            if (!string.Equals(Path.GetExtension(path), expectedExtension, StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(expectedExtension + " 파일만 지원합니다.");
        }
    }
}
