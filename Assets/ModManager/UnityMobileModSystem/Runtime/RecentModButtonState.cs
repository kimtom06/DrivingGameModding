using UnityEngine;
using UnityEngine.UI;

namespace MobileModSystem
{
    /// <summary>
    /// 최근 모드 존재 여부에 따라 버튼 활성화 상태와 표시 문구를 갱신합니다.
    /// 버튼 OnClick에는 MobileModController.LoadRecentModPackage를 연결하세요.
    /// </summary>
    public sealed class RecentModButtonState : MonoBehaviour
    {
        public MobileModController controller;
        public Button button;
        public Text label;

        public string availableText = "최근 모드 불러오기";
        public string unavailableText = "최근 모드 없음";

        private bool isListening;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            AddListener();
            Refresh();
        }

        private void OnDisable()
        {
            RemoveListener();
        }

        public void Refresh()
        {
            SetAvailability(controller != null && controller.HasRecentMod());
        }

        private void AddListener()
        {
            if (isListening || controller == null)
                return;

            controller.onRecentModAvailabilityChanged.AddListener(SetAvailability);
            isListening = true;
        }

        private void RemoveListener()
        {
            if (!isListening)
                return;

            if (controller != null)
                controller.onRecentModAvailabilityChanged.RemoveListener(SetAvailability);

            isListening = false;
        }

        private void SetAvailability(bool available)
        {
            if (button != null)
                button.interactable = available;

            if (label != null)
                label.text = available ? availableText : unavailableText;
        }
    }
}
