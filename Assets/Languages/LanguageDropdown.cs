using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LegacyLanguageDropdown : MonoBehaviour
{
    [Serializable]
    public class LanguageOption
    {
        public string displayName;
        public string languageCode;
    }


    [Header("Legacy Dropdown")]
    public Dropdown dropdown;


    [Header("Languages")]
    public List<LanguageOption> languages =
        new List<LanguageOption>()
        {
            new LanguageOption()
            {
                displayName = "한국어",
                languageCode = "ko"
            },

            new LanguageOption()
            {
                displayName = "English",
                languageCode = "en"
            },

            new LanguageOption()
            {
                displayName = "日本語",
                languageCode = "ja"
            }
        };


    private bool initialized = false;


    private void Start()
    {
        Initialize();
    }


    public void Initialize()
    {
        if (dropdown == null)
        {
            dropdown =
                GetComponent<Dropdown>();
        }

        if (dropdown == null)
        {
            Debug.LogError(
                "[LanguageDropdown] Legacy Dropdown이 없습니다.",
                this
            );

            return;
        }


        // ==========================================
        // Dropdown 옵션 생성
        // ==========================================

        dropdown.ClearOptions();

        List<string> optionNames =
            new List<string>();

        foreach (
            LanguageOption language
            in languages)
        {
            optionNames.Add(
                language.displayName
            );
        }

        dropdown.AddOptions(
            optionNames
        );


        // ==========================================
        // 현재 언어 확인
        // ==========================================

        string currentLanguage =
            "ko";

        if (
            LocalizationManager.Instance
            != null)
        {
            currentLanguage =
                LocalizationManager
                    .Instance
                    .CurrentLanguage;
        }


        int index =
            FindLanguageIndex(
                currentLanguage
            );


        dropdown.SetValueWithoutNotify(
            index
        );

        dropdown.RefreshShownValue();


        // ==========================================
        // 이벤트
        // ==========================================

        dropdown.onValueChanged.RemoveListener(
            OnLanguageChanged
        );

        dropdown.onValueChanged.AddListener(
            OnLanguageChanged
        );


        initialized = true;

        Debug.Log(
            "[LanguageDropdown] Initialized. Current = " +
            currentLanguage
        );
    }


    private void OnLanguageChanged(
        int index)
    {
        if (!initialized)
            return;

        if (
            index < 0 ||
            index >= languages.Count)
        {
            return;
        }


        string languageCode =
            languages[index]
                .languageCode;


        Debug.Log(
            "[LanguageDropdown] Selected = " +
            languageCode
        );


        if (
            LocalizationManager.Instance
            == null)
        {
            Debug.LogError(
                "[LanguageDropdown] LocalizationManager.Instance가 없습니다."
            );

            return;
        }


        LocalizationManager
            .Instance
            .SetLanguage(
                languageCode
            );
    }


    private int FindLanguageIndex(
        string languageCode)
    {
        for (
            int i = 0;
            i < languages.Count;
            i++)
        {
            if (
                string.Equals(
                    languages[i]
                        .languageCode,
                    languageCode,
                    StringComparison
                        .OrdinalIgnoreCase
                ))
            {
                return i;
            }
        }


        // 못 찾으면 한국어
        for (
            int i = 0;
            i < languages.Count;
            i++)
        {
            if (
                string.Equals(
                    languages[i]
                        .languageCode,
                    "ko",
                    StringComparison
                        .OrdinalIgnoreCase
                ))
            {
                return i;
            }
        }


        return 0;
    }


    private void OnDestroy()
    {
        if (dropdown != null)
        {
            dropdown
                .onValueChanged
                .RemoveListener(
                    OnLanguageChanged
                );
        }
    }
}