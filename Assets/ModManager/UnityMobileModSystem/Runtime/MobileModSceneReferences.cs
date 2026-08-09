using UnityEngine;

namespace MobileModSystem
{
    /// <summary>
    /// 씬마다 하나씩 배치하는 참조 제공자입니다.
    /// DontDestroyOnLoad로 유지되는 MobileModController가 새 씬의 부모 오브젝트를 찾을 때 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileModSceneReferences : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("임포트한 모드가 생성될 부모입니다. 비워두면 이 컴포넌트가 붙은 Transform을 사용합니다.")]
        public Transform importedModsParent;

        [Tooltip("현재 씬에서 새 모드를 제작할 루트입니다. 제작 기능을 사용하지 않는 씬에서는 비워둘 수 있습니다.")]
        public Transform buildRoot;

        [Tooltip("기존 모드를 편집용으로 열 때 생성할 부모입니다. 비워두면 buildRoot, buildRoot도 없으면 이 Transform을 사용합니다.")]
        public Transform editWorkspaceParent;

        public Transform ResolveImportedModsParent()
        {
            return importedModsParent != null
                ? importedModsParent
                : transform;
        }

        public Transform ResolveBuildRoot()
        {
            return buildRoot;
        }

        public Transform ResolveEditWorkspaceParent()
        {
            if (editWorkspaceParent != null)
                return editWorkspaceParent;

            if (buildRoot != null)
                return buildRoot;

            return transform;
        }

        private void Reset()
        {
            importedModsParent = transform;
        }
    }
}
