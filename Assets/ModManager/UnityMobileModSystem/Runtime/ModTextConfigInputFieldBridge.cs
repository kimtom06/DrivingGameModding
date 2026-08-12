using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MobileModSystem
{
    /// <summary>
    /// TMP_InputField와 MobileModController를 연결하는
    /// 한글 / 영어 인게임 설정 텍스트 에디터.
    ///
    /// 지원:
    /// - Korean / English IME
    /// - Multiline
    /// - MobileModController 실시간 설정 동기화
    /// - Controller 외부 변경 반영
    /// - Default Settings File 자동 사용
    /// - Ctrl/Cmd + Z Undo
    /// - Ctrl + Y / Cmd + Shift + Z Redo
    /// </summary>
    public sealed class ModTextConfigInputFieldBridge : MonoBehaviour
    {
        // =========================================================
        // References
        // =========================================================

        [Header("References")]
        public MobileModController controller;
        public TMP_InputField inputField;


        // =========================================================
        // Multiline
        // =========================================================

        [Header("Multiline Settings")]
        public bool forceMultiline = true;
        public bool alignTopLeft = true;


        // =========================================================
        // Undo / Redo
        // =========================================================

        [Header("Undo / Redo")]
        [Min(1)]
        public int maxUndoSteps = 100;


        // =========================================================
        // Internal
        // =========================================================

        private bool isListening;

        // InputField -> Controller 전송 중
        private bool isUpdatingFromInputField;

        // Controller -> InputField 적용 중
        private bool isApplyingExternalText;

        // Undo / Redo 적용 중
        private bool isApplyingUndoRedo;


        // =========================================================
        // Undo Data
        // =========================================================

        private readonly Stack<TextState> undoStack =
            new Stack<TextState>();

        private readonly Stack<TextState> redoStack =
            new Stack<TextState>();


        private TextState lastState;


        private struct TextState
        {
            public string text;
            public int caret;

            public TextState(
                string text,
                int caret)
            {
                this.text =
                    text ?? string.Empty;

                this.caret =
                    caret;
            }
        }


        // =========================================================
        // IME
        // =========================================================

        private bool IsIMEComposing
        {
            get
            {
                return !string.IsNullOrEmpty(
                    Input.compositionString
                );
            }
        }


        // =========================================================
        // Unity
        // =========================================================

        private void Awake()
        {
            ResolveReferences();
            ConfigureInputField();
        }


        private void OnEnable()
        {
            ResolveReferences();

            ConfigureInputField();

            AddListeners();


            if (inputField != null)
            {
                lastState =
                    CaptureState();
            }


            // =====================================================
            // 현재 모드 설정 요청
            //
            // 새 모드라면 MobileModController 내부에서
            // defaultSettingsFile이 자동으로 기본값이 됩니다.
            // =====================================================

            if (controller != null)
            {
                controller.RequestCurrentSettingsText();
            }
        }


        private void OnDisable()
        {
            RemoveListeners();
        }


#if UNITY_EDITOR

        private void OnValidate()
        {
            if (inputField == null)
            {
                inputField =
                    GetComponent<TMP_InputField>();
            }


            maxUndoSteps =
                Mathf.Max(
                    1,
                    maxUndoSteps
                );


            ConfigureInputField();
        }

#endif


        // =========================================================
        // References
        // =========================================================

        private void ResolveReferences()
        {
            if (inputField == null)
            {
                inputField =
                    GetComponent<TMP_InputField>();
            }


            if (controller == null)
            {
                controller =
                    MobileModController.Instance;
            }
        }


        // =========================================================
        // InputField Setup
        // =========================================================

        private void ConfigureInputField()
        {
            if (inputField == null)
                return;


            inputField.contentType =
                TMP_InputField.ContentType.Standard;


            inputField.characterValidation =
                TMP_InputField.CharacterValidation.None;


            inputField.characterLimit = 0;


            if (forceMultiline)
            {
                inputField.lineType =
                    TMP_InputField.LineType.MultiLineNewline;
            }


            // 코드 / 설정 텍스트이므로
            // <color> 등을 TMP 태그로 해석하지 않음
            inputField.richText = false;


            if (inputField.textComponent != null)
            {
                inputField.textComponent.enableWordWrapping =
                    true;


                if (alignTopLeft)
                {
                    inputField.textComponent.alignment =
                        TextAlignmentOptions.TopLeft;
                }
            }
        }


        // =========================================================
        // Listener
        // =========================================================

        private void AddListeners()
        {
            if (isListening)
                return;


            if (inputField == null)
                return;


            inputField.onValueChanged.AddListener(
                OnUserEditedText
            );


            inputField.onEndEdit.AddListener(
                OnEndEdit
            );


            if (controller != null)
            {
                controller.onSettingsTextChanged.AddListener(
                    RefreshWithoutNotify
                );
            }


            isListening = true;
        }


        private void RemoveListeners()
        {
            if (!isListening)
                return;


            if (inputField != null)
            {
                inputField.onValueChanged.RemoveListener(
                    OnUserEditedText
                );


                inputField.onEndEdit.RemoveListener(
                    OnEndEdit
                );
            }


            if (controller != null)
            {
                controller.onSettingsTextChanged.RemoveListener(
                    RefreshWithoutNotify
                );
            }


            isListening = false;
        }


        // =========================================================
        // User -> Controller
        // =========================================================

        private void OnUserEditedText(
            string text)
        {
            if (isApplyingExternalText ||
                isApplyingUndoRedo)
            {
                return;
            }


            string normalized =
                NormalizeNewlines(
                    text
                );


            // =====================================================
            // 한글 IME 조합 중
            //
            // ㄱ
            // 가
            // 간
            //
            // 중간 상태에서는 Undo 기록이나 Controller 동기화를
            // 하지 않습니다.
            //
            // TMP가 composition 처리를 전적으로 담당합니다.
            // =====================================================

            if (IsIMEComposing)
                return;


            // =====================================================
            // Undo 기록
            // =====================================================

            TextState current =
                new TextState(
                    normalized,
                    GetSafeCaret()
                );


            if (current.text !=
                lastState.text)
            {
                PushUndo(
                    lastState
                );


                redoStack.Clear();


                lastState =
                    current;
            }


            // =====================================================
            // 기존 스크립트 기능:
            //
            // 현재 텍스트를 MobileModController에 실시간 저장.
            //
            // controller에서 onSettingsTextChanged가 즉시 발생해도
            // isUpdatingFromInputField가 true이므로
            // InputField를 다시 수정하지 않습니다.
            // =====================================================

            if (controller == null)
                return;


            isUpdatingFromInputField =
                true;


            try
            {
                controller.SetCurrentSettingsText(
                    normalized
                );
            }
            finally
            {
                isUpdatingFromInputField =
                    false;
            }
        }


        // =========================================================
        // End Edit
        // =========================================================

        private void OnEndEdit(
            string text)
        {
            if (isApplyingExternalText ||
                isApplyingUndoRedo)
            {
                return;
            }


            string normalized =
                NormalizeNewlines(
                    text
                );


            lastState =
                new TextState(
                    normalized,
                    GetSafeCaret()
                );


            // 마지막 확정 상태도 Controller에 저장
            if (controller != null)
            {
                isUpdatingFromInputField =
                    true;


                try
                {
                    controller.SetCurrentSettingsText(
                        normalized
                    );
                }
                finally
                {
                    isUpdatingFromInputField =
                        false;
                }
            }
        }


        // =========================================================
        // Controller -> InputField
        // =========================================================

        public void RefreshWithoutNotify(
            string text)
        {
            if (inputField == null)
                return;


            // =====================================================
            // 우리가 Controller로 값을 보내서 발생한
            // onSettingsTextChanged는 다시 InputField에 적용하지 않음.
            // =====================================================

            if (isUpdatingFromInputField)
                return;


            // =====================================================
            // 사용자가 현재 타이핑 중이면 외부 텍스트 적용 금지.
            //
            // 이것이 한글 IME 커서 보호에 중요합니다.
            // =====================================================

            if (inputField.isFocused)
                return;


            string normalized =
                NormalizeNewlines(
                    text
                );


            if (inputField.text ==
                normalized)
            {
                lastState =
                    CaptureState();

                return;
            }


            isApplyingExternalText =
                true;


            try
            {
                inputField.SetTextWithoutNotify(
                    normalized
                );


                inputField.ForceLabelUpdate();


                lastState =
                    new TextState(
                        normalized,
                        normalized.Length
                    );
            }
            finally
            {
                isApplyingExternalText =
                    false;
            }


            // 다른 모드 / 새 설정이 로드된 것으로 간주
            undoStack.Clear();
            redoStack.Clear();
        }


        // =========================================================
        // Current Mod Settings Request
        // =========================================================

        /// <summary>
        /// 기존 스크립트의 RequestCurrentText 기능.
        ///
        /// 새 모드:
        /// defaultSettingsFile 기반 텍스트
        ///
        /// 기존 모드:
        /// 해당 RuntimeModTextConfig 텍스트
        ///
        /// 를 가져옵니다.
        /// </summary>
        public void RequestCurrentText()
        {
            ResolveReferences();


            if (controller == null ||
                inputField == null)
            {
                return;
            }


            // 현재 편집 중이면 덮어쓰지 않음
            if (inputField.isFocused)
                return;


            RefreshWithoutNotify(
                controller.GetCurrentSettingsText()
            );
        }


        // =========================================================
        // Save
        // =========================================================

        /// <summary>
        /// 현재 InputField 내용을 강제로
        /// 현재 모드 설정에 저장.
        ///
        /// Export 버튼을 누르기 전에 호출해도 됩니다.
        /// </summary>
        public void SaveCurrentText()
        {
            ResolveReferences();


            if (controller == null ||
                inputField == null)
            {
                return;
            }


            string normalized =
                NormalizeNewlines(
                    inputField.text
                );


            isUpdatingFromInputField =
                true;


            try
            {
                controller.SetCurrentSettingsText(
                    normalized
                );
            }
            finally
            {
                isUpdatingFromInputField =
                    false;
            }


            lastState =
                new TextState(
                    normalized,
                    GetSafeCaret()
                );
        }


        // =========================================================
        // Reset Default Settings
        // =========================================================

        /// <summary>
        /// MobileModController의
        /// defaultSettingsFile/defaultSettingsText로 초기화.
        ///
        /// UI Reset 버튼에 연결 가능.
        /// </summary>
        public void ResetCurrentText()
        {
            ResolveReferences();


            if (controller == null)
                return;


            // Controller가 직접:
            //
            // defaultSettingsFile
            // ↓
            // defaultSettingsText
            //
            // 순서로 기본 설정을 결정합니다.
            controller.ResetCurrentSettingsText();


            // Reset 이벤트가 focus 때문에 무시됐을 가능성까지 고려해서
            // 현재 Controller 값을 직접 적용.
            string current =
                controller.GetCurrentSettingsText();


            ApplyTextForced(
                current
            );


            undoStack.Clear();
            redoStack.Clear();
        }


        // =========================================================
        // Force Current Controller Text
        // =========================================================

        /// <summary>
        /// 현재 Controller의 설정을 강제로 다시 Editor에 표시.
        ///
        /// 기존 모드를 열고 UI를 즉시 새로고침하고 싶을 때 사용 가능.
        /// </summary>
        public void ReloadFromCurrentMod()
        {
            ResolveReferences();


            if (controller == null ||
                inputField == null)
            {
                return;
            }


            ApplyTextForced(
                controller.GetCurrentSettingsText()
            );


            undoStack.Clear();
            redoStack.Clear();
        }


        private void ApplyTextForced(
            string text)
        {
            if (inputField == null)
                return;


            string normalized =
                NormalizeNewlines(
                    text
                );


            isApplyingExternalText =
                true;


            try
            {
                inputField.SetTextWithoutNotify(
                    normalized
                );


                inputField.ForceLabelUpdate();


                int caret =
                    Mathf.Clamp(
                        normalized.Length,
                        0,
                        normalized.Length
                    );


                // Focus 중 강제 reload 함수에서만 사용.
                // 일반 타이핑에서는 절대 실행되지 않습니다.
                inputField.caretPosition =
                    caret;


                inputField.selectionAnchorPosition =
                    caret;


                inputField.selectionFocusPosition =
                    caret;


                lastState =
                    new TextState(
                        normalized,
                        caret
                    );
            }
            finally
            {
                isApplyingExternalText =
                    false;
            }
        }


        // =========================================================
        // Keyboard Undo / Redo
        // =========================================================

        private void OnGUI()
        {
            if (inputField == null)
                return;


            if (!inputField.isFocused)
                return;


            // IME 조합 중에는 Undo 금지
            if (IsIMEComposing)
                return;


            Event current =
                Event.current;


            if (current == null)
                return;


            if (current.type !=
                EventType.KeyDown)
            {
                return;
            }


            bool modifier =
                current.control ||
                current.command;


            if (!modifier)
                return;


            // =====================================================
            // Cmd/Ctrl + Shift + Z
            // =====================================================

            if (current.keyCode ==
                    KeyCode.Z &&
                current.shift)
            {
                Redo();

                current.Use();

                return;
            }


            // =====================================================
            // Cmd/Ctrl + Z
            // =====================================================

            if (current.keyCode ==
                KeyCode.Z)
            {
                Undo();

                current.Use();

                return;
            }


            // =====================================================
            // Ctrl + Y
            // =====================================================

            if (current.keyCode ==
                KeyCode.Y)
            {
                Redo();

                current.Use();
            }
        }


        // =========================================================
        // Undo
        // =========================================================

        public void Undo()
        {
            if (inputField == null)
                return;


            if (IsIMEComposing)
                return;


            if (undoStack.Count == 0)
                return;


            TextState current =
                CaptureState();


            redoStack.Push(
                current
            );


            TextState previous =
                undoStack.Pop();


            ApplyUndoRedoState(
                previous
            );
        }


        // =========================================================
        // Redo
        // =========================================================

        public void Redo()
        {
            if (inputField == null)
                return;


            if (IsIMEComposing)
                return;


            if (redoStack.Count == 0)
                return;


            TextState current =
                CaptureState();


            PushUndo(
                current
            );


            TextState next =
                redoStack.Pop();


            ApplyUndoRedoState(
                next
            );
        }


        // =========================================================
        // Apply Undo / Redo
        // =========================================================

        private void ApplyUndoRedoState(
            TextState state)
        {
            if (inputField == null)
                return;


            if (IsIMEComposing)
                return;


            string normalized =
                NormalizeNewlines(
                    state.text
                );


            int caret =
                Mathf.Clamp(
                    state.caret,
                    0,
                    normalized.Length
                );


            isApplyingUndoRedo =
                true;


            try
            {
                inputField.SetTextWithoutNotify(
                    normalized
                );


                inputField.caretPosition =
                    caret;


                inputField.selectionAnchorPosition =
                    caret;


                inputField.selectionFocusPosition =
                    caret;


                inputField.ForceLabelUpdate();


                lastState =
                    new TextState(
                        normalized,
                        caret
                    );
            }
            finally
            {
                isApplyingUndoRedo =
                    false;
            }


            // =====================================================
            // Undo / Redo 결과도 현재 모드 설정에 바로 저장
            // =====================================================

            if (controller != null)
            {
                isUpdatingFromInputField =
                    true;


                try
                {
                    controller.SetCurrentSettingsText(
                        normalized
                    );
                }
                finally
                {
                    isUpdatingFromInputField =
                        false;
                }
            }
        }


        // =========================================================
        // State
        // =========================================================

        private TextState CaptureState()
        {
            if (inputField == null)
            {
                return new TextState(
                    string.Empty,
                    0
                );
            }


            return new TextState(
                NormalizeNewlines(
                    inputField.text
                ),

                GetSafeCaret()
            );
        }


        private int GetSafeCaret()
        {
            if (inputField == null)
                return 0;


            int length =
                string.IsNullOrEmpty(
                    inputField.text
                )
                    ? 0
                    : inputField.text.Length;


            return Mathf.Clamp(
                inputField.caretPosition,
                0,
                length
            );
        }


        // =========================================================
        // Undo Stack
        // =========================================================

        private void PushUndo(
            TextState state)
        {
            if (undoStack.Count > 0)
            {
                TextState top =
                    undoStack.Peek();


                if (top.text ==
                        state.text &&
                    top.caret ==
                        state.caret)
                {
                    return;
                }
            }


            undoStack.Push(
                state
            );


            TrimUndoStack();
        }


        private void TrimUndoStack()
        {
            if (undoStack.Count <=
                maxUndoSteps)
            {
                return;
            }


            TextState[] states =
                undoStack.ToArray();


            undoStack.Clear();


            int count =
                Mathf.Min(
                    maxUndoSteps,
                    states.Length
                );


            for (int i = count - 1;
                 i >= 0;
                 i--)
            {
                undoStack.Push(
                    states[i]
                );
            }
        }


        // =========================================================
        // Public Utility
        // =========================================================

        public void ClearUndoHistory()
        {
            undoStack.Clear();
            redoStack.Clear();


            if (inputField != null)
            {
                lastState =
                    CaptureState();
            }
        }


        public bool CanUndo()
        {
            return undoStack.Count > 0;
        }


        public bool CanRedo()
        {
            return redoStack.Count > 0;
        }


        // =========================================================
        // Utility
        // =========================================================

        private static string NormalizeNewlines(
            string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }
    }
}