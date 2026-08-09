using UnityEngine;

namespace MobileModSystem
{
    /// <summary>
    /// 기존 모드팩을 편집용으로 다시 열었을 때 원래 모드 ID와 메타데이터를 유지합니다.
    /// 이 컴포넌트는 게임 동작 코드를 불러오는 용도가 아니라 내보내기 메타데이터 보존용입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeModPackageIdentity : MonoBehaviour
    {
        [SerializeField] private string modId;
        [SerializeField] private string displayName;
        [SerializeField] private string author;

        public string ModId => modId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string Author => author ?? string.Empty;

        public void Initialize(string sourceModId, string sourceDisplayName, string sourceAuthor)
        {
            modId = sourceModId ?? string.Empty;
            displayName = sourceDisplayName ?? string.Empty;
            author = sourceAuthor ?? string.Empty;
        }

        public void UpdateMetadata(string newDisplayName, string newAuthor)
        {
            displayName = newDisplayName ?? string.Empty;
            author = newAuthor ?? string.Empty;
        }
    }
}
