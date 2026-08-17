using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LocalizedUIText : MonoBehaviour
{
    [SerializeField, HideInInspector]
    private string localizationId;

    [SerializeField, TextArea(2, 10)]
    private string koreanText;

    [Tooltip("체크하면 번역 대상에서 제외됩니다.")]
    public bool ignoreLocalization = false;

    public string Id => localizationId;
    public string KoreanText => koreanText;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        LocalizationManager.TryApply(this);
    }

    public string GetCurrentText()
    {
        TMP_Text tmp = GetComponent<TMP_Text>();

        if (tmp != null)
            return tmp.text;

        Text legacy = GetComponent<Text>();

        if (legacy != null)
            return legacy.text;

        return "";
    }

    public void SetText(string value)
    {
        if (ignoreLocalization)
            return;

        TMP_Text tmp = GetComponent<TMP_Text>();

        if (tmp != null)
        {
            tmp.text = value;
            return;
        }

        Text legacy = GetComponent<Text>();

        if (legacy != null)
            legacy.text = value;
    }

#if UNITY_EDITOR

    public bool EditorEnsureId()
    {
        if (!string.IsNullOrEmpty(localizationId))
            return false;

        localizationId = Guid.NewGuid().ToString("N");

        return true;
    }

    public void EditorCaptureKorean()
    {
        koreanText = GetCurrentText();
    }

#endif
}