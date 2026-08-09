using UnityEngine;

namespace MobileModSystem
{
    // 한 오브젝트에 여러 텍스처를 적용할 수 있으므로 중복 컴포넌트를 허용합니다.
    public sealed class RuntimeTextureBinding : MonoBehaviour
    {
        [Tooltip("Mod workspace에 복사된 원본 PNG/JPG 파일 경로")]
        public string sourceFilePath;
        public Renderer targetRenderer;
        public int materialIndex;
        public string propertyName = "_BaseMap";
    }
}
