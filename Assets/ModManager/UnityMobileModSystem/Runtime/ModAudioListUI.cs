using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MobileModSystem
{
    /// <summary>
    /// Displays all RuntimeAudioBinding entries in the current/imported mod and
    /// allows individual audio entries to be removed from the mod.
    /// Removing an entry destroys its AudioSource + RuntimeAudioBinding, so the
    /// next .sdgmod export will no longer include that audio.
    /// </summary>
    public sealed class ModAudioListUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MobileModController controller;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private ModAudioListItemUI itemPrefab;
        [SerializeField] private Text emptyText;

        [Header("Target")]
        [Tooltip("Optional explicit mod root. If empty, currentEditableModRoot or buildRoot is used.")]
        [SerializeField] private GameObject targetRoot;

        [Tooltip("Automatically switch the list to the mod that was most recently imported/opened for editing.")]
        [SerializeField] private bool followControllerImports = true;

        [Tooltip("Refresh the list whenever this UI object is enabled.")]
        [SerializeField] private bool refreshOnEnable = true;

        [Header("Auto Refresh")]
        [Tooltip("Automatically detect RuntimeAudioBinding/AudioSource additions, removals, and replacements while this UI is active.")]
        [SerializeField] private bool autoDetectAudioChanges = true;

        [Tooltip("How often to check for audio list changes. 0.2 seconds is usually enough for UI.")]
        [SerializeField, Min(0.05f)] private float autoRefreshInterval = 0.2f;

        [Header("Delete")]
        [Tooltip("Destroy the runtime AudioClip too when no other audio binding in this mod uses it.")]
        [SerializeField] private bool destroyUnusedAudioClip = true;

        [Header("Events")]
        public UnityEvent<int> onAudioCountChanged;
        public UnityEvent<string> onAudioDeleted;

        private readonly List<ModAudioListItemUI> spawnedItems =
            new List<ModAudioListItemUI>();

        private bool subscribed;
        private Coroutine autoRefreshCoroutine;
        private int lastAudioStateSignature = int.MinValue;

        public GameObject TargetRoot => ResolveTargetRoot();

        public int AudioCount
        {
            get
            {
                GameObject root = ResolveTargetRoot();
                if (root == null)
                    return 0;

                return GetValidBindings(root).Count;
            }
        }

        private void Awake()
        {
            ResolveController();
        }

        private void OnEnable()
        {
            ResolveController();
            SubscribeControllerEvents();

            if (refreshOnEnable)
                RefreshAudioList();
            else
                UpdateCachedAudioState();

            StartAutoRefreshIfNeeded();
        }

        private void OnDisable()
        {
            StopAutoRefresh();
            UnsubscribeControllerEvents();
        }

        /// <summary>
        /// Useful for MobileModController import-complete UnityEvents.
        /// Connect Dynamic GameObject -> ModAudioListUI.SetTargetRoot.
        /// </summary>
        public void SetTargetRoot(GameObject root)
        {
            targetRoot = root;
            RefreshAudioList();
        }

        /// <summary>
        /// Clears the explicit target and goes back to following the controller's
        /// current editable/build root.
        /// </summary>
        public void UseCurrentControllerMod()
        {
            targetRoot = null;
            RefreshAudioList();
        }

        /// <summary>
        /// Rebuilds the visible audio list from RuntimeAudioBinding components.
        /// Can be connected directly to a UI Button.
        /// </summary>
        public void RefreshAudioList()
        {
            ClearSpawnedItems();

            if (contentRoot == null)
            {
                Debug.LogError("ModAudioListUI: contentRoot is not assigned.", this);
                SetEmptyState(true, "Audio list content is not assigned.");
                onAudioCountChanged?.Invoke(0);
                return;
            }

            if (itemPrefab == null)
            {
                Debug.LogError("ModAudioListUI: itemPrefab is not assigned.", this);
                SetEmptyState(true, "Audio list item prefab is not assigned.");
                onAudioCountChanged?.Invoke(0);
                return;
            }

            GameObject root = ResolveTargetRoot();
            if (root == null)
            {
                SetEmptyState(true, "No mod selected");
                onAudioCountChanged?.Invoke(0);
                return;
            }

            List<RuntimeAudioBinding> bindings = GetValidBindings(root);
            bindings.Sort(CompareBindingsByName);

            foreach (RuntimeAudioBinding binding in bindings)
            {
                ModAudioListItemUI item = Instantiate(itemPrefab, contentRoot);
                item.gameObject.SetActive(true);
                item.Bind(this, binding);
                spawnedItems.Add(item);
            }

            SetEmptyState(bindings.Count == 0, "No imported audio");
            onAudioCountChanged?.Invoke(bindings.Count);

            // Cache the exact current binding/source/clip identity. This catches
            // remove-one/add-one cases even when the total audio count is unchanged.
            lastAudioStateSignature = CalculateAudioStateSignature(root);
        }

        /// <summary>
        /// Deletes one imported audio entry from the current mod.
        /// The workspace source file is intentionally left on disk because another
        /// mod/binding may still reference the same cached file.
        /// </summary>
        public void DeleteAudio(RuntimeAudioBinding binding)
        {
            if (binding == null)
                return;

            GameObject root = ResolveTargetRoot();
            if (root == null)
                return;

            if (!binding.transform.IsChildOf(root.transform) &&
                binding.gameObject != root)
            {
                Debug.LogWarning(
                    "ModAudioListUI: Tried to delete an audio binding outside the selected mod.",
                    binding);
                return;
            }

            string displayName = GetDisplayName(binding);
            AudioSource source = binding.targetAudioSource;
            AudioClip clip = source != null ? source.clip : null;

            bool clipUsedElsewhere = clip != null &&
                                     IsClipUsedByAnotherBinding(root, binding, clip);

            if (source != null)
            {
                source.Stop();
                source.clip = null;
                Destroy(source);
            }

            // Destroying this component is what removes the audio from the next export.
            Destroy(binding);

            if (destroyUnusedAudioClip && clip != null && !clipUsedElsewhere)
                Destroy(clip);

            onAudioDeleted?.Invoke(displayName);

            // Destroy(Component) is applied at the end of the frame, so rebuild the
            // list on the next frame to avoid showing the deleted binding again.
            StartCoroutine(RefreshNextFrame());
        }

        /// <summary>
        /// Deletes every imported audio entry under the selected mod.
        /// Can be connected to a "Delete All Audio" button if desired.
        /// </summary>
        public void DeleteAllAudio()
        {
            GameObject root = ResolveTargetRoot();
            if (root == null)
                return;

            List<RuntimeAudioBinding> bindings = GetValidBindings(root);
            if (bindings.Count == 0)
                return;

            HashSet<AudioClip> clipsToDestroy = new HashSet<AudioClip>();

            foreach (RuntimeAudioBinding binding in bindings)
            {
                if (binding == null)
                    continue;

                AudioSource source = binding.targetAudioSource;
                if (source != null)
                {
                    if (source.clip != null)
                        clipsToDestroy.Add(source.clip);

                    source.Stop();
                    source.clip = null;
                    Destroy(source);
                }

                Destroy(binding);
            }

            if (destroyUnusedAudioClip)
            {
                foreach (AudioClip clip in clipsToDestroy)
                {
                    if (clip != null)
                        Destroy(clip);
                }
            }

            onAudioCountChanged?.Invoke(0);
            StartCoroutine(RefreshNextFrame());
        }

        public string GetAudioName(RuntimeAudioBinding binding)
        {
            return GetDisplayName(binding);
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            RefreshAudioList();
        }

        private void StartAutoRefreshIfNeeded()
        {
            StopAutoRefresh();

            if (!autoDetectAudioChanges || !isActiveAndEnabled)
                return;

            autoRefreshCoroutine = StartCoroutine(AutoRefreshRoutine());
        }

        private void StopAutoRefresh()
        {
            if (autoRefreshCoroutine == null)
                return;

            StopCoroutine(autoRefreshCoroutine);
            autoRefreshCoroutine = null;
        }

        private IEnumerator AutoRefreshRoutine()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(
                Mathf.Max(0.05f, autoRefreshInterval));

            while (true)
            {
                yield return wait;

                GameObject root = ResolveTargetRoot();
                int currentSignature = CalculateAudioStateSignature(root);

                if (currentSignature == lastAudioStateSignature)
                    continue;

                RefreshAudioList();
            }
        }

        private void UpdateCachedAudioState()
        {
            lastAudioStateSignature = CalculateAudioStateSignature(ResolveTargetRoot());
        }

        /// <summary>
        /// Generates an order-independent signature using the actual binding,
        /// AudioSource and AudioClip instance IDs. A delete+add operation is detected
        /// even if the number of audio entries stays exactly the same.
        /// </summary>
        private static int CalculateAudioStateSignature(GameObject root)
        {
            if (root == null)
                return 0;

            RuntimeAudioBinding[] bindings =
                root.GetComponentsInChildren<RuntimeAudioBinding>(true);

            unchecked
            {
                int signature = 17;
                int validCount = 0;
                int identityMix = 0;

                foreach (RuntimeAudioBinding binding in bindings)
                {
                    if (binding == null || binding.targetAudioSource == null)
                        continue;

                    validCount++;

                    int bindingId = binding.GetInstanceID();
                    int sourceId = binding.targetAudioSource.GetInstanceID();
                    int clipId = binding.targetAudioSource.clip != null
                        ? binding.targetAudioSource.clip.GetInstanceID()
                        : 0;

                    // XOR keeps the result independent of component enumeration order.
                    int entryHash = bindingId;
                    entryHash = (entryHash * 397) ^ sourceId;
                    entryHash = (entryHash * 397) ^ clipId;
                    identityMix ^= entryHash;
                }

                signature = (signature * 31) ^ validCount;
                signature = (signature * 31) ^ identityMix;
                return signature;
            }
        }

        private GameObject ResolveTargetRoot()
        {
            if (targetRoot != null)
                return targetRoot;

            ResolveController();
            if (controller == null)
                return null;

            if (controller.currentEditableModRoot != null)
                return controller.currentEditableModRoot;

            if (controller.buildRoot != null)
                return controller.buildRoot.gameObject;

            return null;
        }

        private void ResolveController()
        {
            if (controller == null && MobileModController.HasInstance)
                controller = MobileModController.Instance;
        }

        private void SubscribeControllerEvents()
        {
            if (!followControllerImports || subscribed)
                return;

            ResolveController();
            if (controller == null)
                return;

            controller.onModPackageImportCompleted?.AddListener(HandleImportedMod);
            controller.onRecentModPackageLoadCompleted?.AddListener(HandleImportedMod);
            controller.onEditableModOpened?.AddListener(HandleImportedMod);
            controller.onEditModeChanged?.AddListener(HandleEditModeChanged);
            subscribed = true;
        }

        private void UnsubscribeControllerEvents()
        {
            if (!subscribed || controller == null)
                return;

            controller.onModPackageImportCompleted?.RemoveListener(HandleImportedMod);
            controller.onRecentModPackageLoadCompleted?.RemoveListener(HandleImportedMod);
            controller.onEditableModOpened?.RemoveListener(HandleImportedMod);
            controller.onEditModeChanged?.RemoveListener(HandleEditModeChanged);
            subscribed = false;
        }

        private void HandleImportedMod(GameObject importedRoot)
        {
            targetRoot = importedRoot;
            RefreshAudioList();
        }

        private void HandleEditModeChanged(bool isEditing)
        {
            if (!isEditing)
                targetRoot = null;

            RefreshAudioList();
        }

        private static List<RuntimeAudioBinding> GetValidBindings(GameObject root)
        {
            RuntimeAudioBinding[] found =
                root.GetComponentsInChildren<RuntimeAudioBinding>(true);

            List<RuntimeAudioBinding> result =
                new List<RuntimeAudioBinding>(found.Length);

            foreach (RuntimeAudioBinding binding in found)
            {
                if (binding == null || binding.targetAudioSource == null)
                    continue;

                result.Add(binding);
            }

            return result;
        }

        private static int CompareBindingsByName(
            RuntimeAudioBinding a,
            RuntimeAudioBinding b)
        {
            return string.Compare(
                GetDisplayName(a),
                GetDisplayName(b),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns only the original audio file name for UI display.
        /// Example: EngineIdle.wav
        /// No path, labels, volume, or other details are included here.
        /// </summary>
        public static string GetDisplayName(RuntimeAudioBinding binding)
        {
            if (binding == null)
                return "Unknown Audio";

            // Newer bindings store the exact original file name.
            if (!string.IsNullOrWhiteSpace(binding.originalFileName))
                return Path.GetFileName(binding.originalFileName);

            // Compatibility with bindings created before originalFileName was added.
            // RuntimeModAssetImporter restores AudioClip.name to the original basename,
            // while sourceFilePath still provides the original audio extension.
            AudioClip clip = binding.targetAudioSource != null
                ? binding.targetAudioSource.clip
                : null;

            if (clip != null && !string.IsNullOrWhiteSpace(clip.name))
            {
                string extension = string.Empty;

                if (!string.IsNullOrWhiteSpace(binding.sourceFilePath))
                    extension = Path.GetExtension(binding.sourceFilePath);

                string reconstructedFileName = Path.GetFileName(clip.name);

                if (!string.IsNullOrWhiteSpace(extension) &&
                    !reconstructedFileName.EndsWith(
                        extension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    reconstructedFileName += extension;
                }

                // Backfill the binding so re-export/UI refreshes keep the restored name.
                binding.originalFileName = reconstructedFileName;
                return reconstructedFileName;
            }

            // Last-resort fallback. This can be a workspace/cache filename on very old data.
            if (!string.IsNullOrWhiteSpace(binding.sourceFilePath))
                return Path.GetFileName(binding.sourceFilePath);

            return "Unknown Audio";
        }

        private static bool IsClipUsedByAnotherBinding(
            GameObject root,
            RuntimeAudioBinding deletingBinding,
            AudioClip clip)
        {
            RuntimeAudioBinding[] bindings =
                root.GetComponentsInChildren<RuntimeAudioBinding>(true);

            foreach (RuntimeAudioBinding other in bindings)
            {
                if (other == null || other == deletingBinding)
                    continue;

                AudioSource source = other.targetAudioSource;
                if (source != null && source.clip == clip)
                    return true;
            }

            return false;
        }

        private void ClearSpawnedItems()
        {
            foreach (ModAudioListItemUI item in spawnedItems)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }

            spawnedItems.Clear();
        }

        private void SetEmptyState(bool visible, string message)
        {
            if (emptyText == null)
                return;

            emptyText.gameObject.SetActive(visible);
            if (visible)
                emptyText.text = message;
        }
    }
}
