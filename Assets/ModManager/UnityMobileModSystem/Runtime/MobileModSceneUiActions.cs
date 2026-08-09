using UnityEngine;
using UnityEngine.Events;

namespace MobileModSystem
{
    /// <summary>
    /// 각 씬의 UI Button에 연결하는 로컬 브리지입니다.
    /// DontDestroyOnLoad 싱글톤을 Inspector에서 직접 참조하기 어려울 때 사용합니다.
    /// </summary>
    public sealed class MobileModSceneUiActions : MonoBehaviour
    {
        [Header("Import Completed Events")]
        [Tooltip("파일 선택을 통한 모드 임포트가 성공했을 때 불러온 루트를 전달합니다.")]
        public UnityEvent<GameObject> onPickImportCompleted;

        [Tooltip("최근 모드 불러오기가 성공했을 때 불러온 루트를 전달합니다.")]
        public UnityEvent<GameObject> onRecentImportCompleted;

        public void PickAndImportModPackage()
        {
            MobileModController controller = GetController();
            if (controller == null)
                return;

            controller.PickAndImportModPackage(imported =>
            {
                if (this != null)
                    onPickImportCompleted?.Invoke(imported);
            });
        }

        public void LoadRecentModPackage()
        {
            MobileModController controller = GetController();
            if (controller == null)
                return;

            controller.LoadRecentModPackage(imported =>
            {
                if (this != null)
                    onRecentImportCompleted?.Invoke(imported);
            });
        }

        public void PickAndOpenModForEditing()
        {
            GetController()?.PickAndOpenModForEditing();
        }

        public void OpenRecentModForEditing()
        {
            GetController()?.OpenRecentModForEditing();
        }

        public void ExportCurrentMod()
        {
            GetController()?.ExportCurrentMod();
        }

        public void CloseCurrentModEditing()
        {
            GetController()?.CloseCurrentModEditing();
        }

        public void PickAndImportModel()
        {
            GetController()?.PickAndImportModel();
        }

        public void PickAndApplyAudio()
        {
            GetController()?.PickAndApplyAudio();
        }

        private static MobileModController GetController()
        {
            if (MobileModController.HasInstance)
                return MobileModController.Instance;

            Debug.LogError("MobileModController 싱글톤이 생성되지 않았습니다.");
            return null;
        }
    }
}
