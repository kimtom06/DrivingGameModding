using UnityEngine;

namespace MobileModSystem
{
    [DisallowMultipleComponent]
    public sealed class RuntimeModelBinding : MonoBehaviour
    {
        [Tooltip("Mod workspace에 복사된 원본 .glb 파일 경로")]
        public string sourceFilePath;
    }
}
