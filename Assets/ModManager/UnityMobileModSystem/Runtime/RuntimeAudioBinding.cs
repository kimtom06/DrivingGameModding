using UnityEngine;

namespace MobileModSystem
{
    public sealed class RuntimeAudioBinding : MonoBehaviour
    {
        [Tooltip("Mod workspace에 복사된 원본 WAV/OGG/MP3/AIFF 파일 경로")]
        public string sourceFilePath;

        [Tooltip("사용자가 선택했거나 모드 패키지에 기록된 원래 오디오 파일 이름. 재내보내기 시에도 유지됩니다.")]
        public string originalFileName;

        public AudioSource targetAudioSource;
    }
}
