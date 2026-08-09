using UnityEngine;

namespace MobileModSystem
{
    public sealed class RuntimeAudioBinding : MonoBehaviour
    {
        [Tooltip("Mod workspace에 복사된 원본 WAV/OGG/MP3/AIFF 파일 경로")]
        public string sourceFilePath;
        public AudioSource targetAudioSource;
    }
}
