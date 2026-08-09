using UnityEngine;
using UnityEngine.UI;

namespace MobileModSystem
{
    /// <summary>
    /// Legacy Unity UI InputField와 MobileModController를 연결합니다.
    /// 여러 줄 설정 편집을 지원하며 사용자 입력 중 커서 위치를 유지합니다.
    /// </summary>
    public sealed class ModTextConfigInputFieldBridge : MonoBehaviour
    {
        [Header("References")]
        public MobileModController controller;
        public InputField inputField;

        [Header("Multiline Settings")]
        public bool forceMultiline = true;
        public bool alignTopLeft = true;

        private bool isListening;
        private bool isUpdatingFromInputField;
        private bool isApplyingExternalText;

        private void Awake()
        {
            ConfigureInputField();
        }

        private void OnEnable()
        {
            ConfigureInputField();
            AddListeners();

            if (controller != null)
                controller.RequestCurrentSettingsText();
        }

        private void OnDisable()
        {
            RemoveListeners();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (inputField == null)
                inputField = GetComponent<InputField>();

            ConfigureInputField();
        }
#endif

        private void ConfigureInputField()
        {
            if (inputField == null)
                inputField = GetComponent<InputField>();

            if (inputField == null)
                return;

            inputField.contentType = InputField.ContentType.Standard;
            inputField.characterLimit = 0;

            if (forceMultiline)
                inputField.lineType = InputField.LineType.MultiLineNewline;

            Text textComponent = inputField.textComponent;
            if (textComponent != null)
            {
                textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
                textComponent.verticalOverflow = VerticalWrapMode.Overflow;

                if (alignTopLeft)
                    textComponent.alignment = TextAnchor.UpperLeft;
            }

            inputField.ForceLabelUpdate();
        }

        private void AddListeners()
        {
            if (isListening || controller == null || inputField == null)
                return;

            inputField.onValueChanged.AddListener(OnUserEditedText);
            controller.onSettingsTextChanged.AddListener(RefreshWithoutNotify);
            isListening = true;
        }

        private void RemoveListeners()
        {
            if (!isListening)
                return;

            if (inputField != null)
                inputField.onValueChanged.RemoveListener(OnUserEditedText);

            if (controller != null)
                controller.onSettingsTextChanged.RemoveListener(RefreshWithoutNotify);

            isListening = false;
        }

        private void OnUserEditedText(string text)
        {
            if (isApplyingExternalText || controller == null)
                return;

            isUpdatingFromInputField = true;

            try
            {
                controller.SetCurrentSettingsText(NormalizeNewlines(text));
            }
            finally
            {
                isUpdatingFromInputField = false;
            }
        }

        public void RefreshWithoutNotify(string text)
        {
            if (inputField == null || isUpdatingFromInputField)
                return;

            string normalized = NormalizeNewlines(text);
            if (inputField.text == normalized)
                return;

            bool wasFocused = inputField.isFocused;
            int oldCaretPosition = inputField.caretPosition;
            int oldSelectionAnchor = inputField.selectionAnchorPosition;
            int oldSelectionFocus = inputField.selectionFocusPosition;

            isApplyingExternalText = true;

            try
            {
                inputField.SetTextWithoutNotify(normalized);

                if (wasFocused)
                {
                    int maximumPosition = normalized.Length;
                    inputField.caretPosition = Mathf.Clamp(oldCaretPosition, 0, maximumPosition);
                    inputField.selectionAnchorPosition = Mathf.Clamp(oldSelectionAnchor, 0, maximumPosition);
                    inputField.selectionFocusPosition = Mathf.Clamp(oldSelectionFocus, 0, maximumPosition);
                }

                inputField.ForceLabelUpdate();
            }
            finally
            {
                isApplyingExternalText = false;
            }
        }

        public void RequestCurrentText()
        {
            if (controller != null)
                RefreshWithoutNotify(controller.GetCurrentSettingsText());
        }

        private static string NormalizeNewlines(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }
    }
}
