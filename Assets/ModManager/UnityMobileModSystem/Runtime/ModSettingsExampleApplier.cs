using UnityEngine;

namespace MobileModSystem
{
    /// <summary>
    /// 선택 사항인 사용 예제입니다.
    /// MobileModController.onObjectCreated에 ApplyImportedObject를 연결할 수 있습니다.
    /// 실제 게임에서는 이 스크립트를 복사해 프로젝트 전용 키를 적용하세요.
    /// </summary>
    public sealed class ModSettingsExampleApplier : MonoBehaviour
    {
        public void ApplyImportedObject(GameObject importedRoot)
        {
            if (importedRoot == null)
                return;

            RuntimeModTextConfig config = importedRoot.GetComponent<RuntimeModTextConfig>();
            if (config == null)
                return;

            string displayName = config.GetString("object.displayName", importedRoot.name);
            if (!string.IsNullOrWhiteSpace(displayName))
                importedRoot.name = displayName;

            if (config.TryGetFloat("object.uniformScale", out float scale))
            {
                scale = Mathf.Clamp(scale, 0.01f, 100f);
                importedRoot.transform.localScale *= scale;
            }

            if (config.TryGetVector3("spawn.position", out Vector3 position))
                importedRoot.transform.localPosition = position;

            if (config.TryGetVector3("spawn.rotation", out Vector3 rotation))
                importedRoot.transform.localEulerAngles = rotation;

            if (config.TryGetFloat("audio.volume", out float volume))
            {
                foreach (AudioSource source in importedRoot.GetComponentsInChildren<AudioSource>(true))
                    source.volume = Mathf.Clamp01(volume);
            }

            if (config.TryGetBool("object.enabled", out bool enabled))
                importedRoot.SetActive(enabled);
        }
    }
}
