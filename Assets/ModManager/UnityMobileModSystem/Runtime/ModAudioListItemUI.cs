using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace MobileModSystem
{
    /// <summary>
    /// One row in the imported audio list.
    /// This component displays ONLY the original audio file name.
    /// No extension-only / loop / volume detail string is ever written.
    /// </summary>
    public sealed class ModAudioListItemUI : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Text that displays ONLY the original audio filename, e.g. EngineIdle.wav")]
        [SerializeField] private Text fileNameText;

        [SerializeField] private Button deleteButton;

        private ModAudioListUI owner;
        private RuntimeAudioBinding binding;

        private void Awake()
        {
            ResolveFileNameText();

            if (deleteButton != null)
                deleteButton.onClick.AddListener(DeleteCurrentAudio);
        }

        private void OnDestroy()
        {
            if (deleteButton != null)
                deleteButton.onClick.RemoveListener(DeleteCurrentAudio);
        }

        public void Bind(ModAudioListUI listOwner, RuntimeAudioBinding audioBinding)
        {
            owner = listOwner;
            binding = audioBinding;

            ResolveFileNameText();
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            ResolveFileNameText();

            if (fileNameText == null)
            {
                Debug.LogError(
                    "ModAudioListItemUI: File Name Text could not be found. " +
                    "Assign the filename Text component in the prefab.",
                    this);
                return;
            }

            string fileName = GetOriginalFileName(binding);

            // IMPORTANT: this component writes ONLY the filename.
            fileNameText.text = fileName;
        }

        private static string GetOriginalFileName(RuntimeAudioBinding audioBinding)
        {
            if (audioBinding == null)
                return "Unknown Audio";

            // 1. Exact filename saved when the audio was originally selected/imported.
            if (!string.IsNullOrWhiteSpace(audioBinding.originalFileName))
            {
                return Path.GetFileName(audioBinding.originalFileName);
            }

            AudioClip clip = audioBinding.targetAudioSource != null
                ? audioBinding.targetAudioSource.clip
                : null;

            // 2. Older bindings: rebuild filename from AudioClip.name + source extension.
            if (clip != null && !string.IsNullOrWhiteSpace(clip.name))
            {
                string clipName = Path.GetFileName(clip.name);
                string extension = !string.IsNullOrWhiteSpace(audioBinding.sourceFilePath)
                    ? Path.GetExtension(audioBinding.sourceFilePath)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(extension) &&
                    !clipName.EndsWith(extension, System.StringComparison.OrdinalIgnoreCase))
                {
                    clipName += extension;
                }

                // Backfill so future refresh/export keeps the filename.
                audioBinding.originalFileName = clipName;
                return clipName;
            }

            // 3. Last fallback.
            if (!string.IsNullOrWhiteSpace(audioBinding.sourceFilePath))
                return Path.GetFileName(audioBinding.sourceFilePath);

            return "Unknown Audio";
        }

        /// <summary>
        /// Tries to recover the filename Text automatically when the prefab reference
        /// was not assigned. First prefers child Text objects whose GameObject name
        /// contains "file" or "name", then uses the first Text that is not part of
        /// the delete button.
        /// </summary>
        private void ResolveFileNameText()
        {
            if (fileNameText != null)
                return;

            Text[] texts = GetComponentsInChildren<Text>(true);

            // Prefer an explicitly named filename object.
            foreach (Text text in texts)
            {
                if (text == null)
                    continue;

                string objectName = text.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("filename") ||
                    objectName.Contains("file_name") ||
                    objectName.Contains("file name") ||
                    objectName == "name" ||
                    objectName.Contains("audioname") ||
                    objectName.Contains("audio_name"))
                {
                    fileNameText = text;
                    return;
                }
            }

            // Otherwise use the first Text that is not inside the delete Button.
            foreach (Text text in texts)
            {
                if (text == null)
                    continue;

                if (deleteButton != null &&
                    text.transform.IsChildOf(deleteButton.transform))
                {
                    continue;
                }

                fileNameText = text;
                return;
            }
        }

        public void DeleteCurrentAudio()
        {
            if (owner == null || binding == null)
                return;

            if (deleteButton != null)
                deleteButton.interactable = false;

            owner.DeleteAudio(binding);
        }
    }
}