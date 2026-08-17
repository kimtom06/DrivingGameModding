using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    [Header("Localization File")]
    [Tooltip("Assets/Resources/Localization/Localization.xml")]
    public string xmlResourcePath = "Localization/Localization";

    [Header("Language")]
    public string defaultLanguage = "ko";

    public bool saveLanguage = true;

    public string playerPrefsKey = "SelectedLanguage";

    [Header("Manager")]
    public bool dontDestroyOnLoad = true;

    public string CurrentLanguage { get; private set; } = "ko";

    private readonly Dictionary<string, Dictionary<string, string>>
        localizationData =
            new Dictionary<string, Dictionary<string, string>>();

    private bool loaded = false;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        // XML 먼저 읽기
        LoadLocalization();

        // 저장된 언어 불러오기
        if (saveLanguage)
        {
            CurrentLanguage = PlayerPrefs.GetString(
                playerPrefsKey,
                defaultLanguage
            );
        }
        else
        {
            CurrentLanguage = defaultLanguage;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        ApplyAll();
    }


    // =====================================================
    // XML LOAD
    // =====================================================

    public void LoadLocalization()
    {
        localizationData.Clear();

        TextAsset xmlFile =
            Resources.Load<TextAsset>(
                xmlResourcePath
            );

        if (xmlFile == null)
        {
            Debug.LogError(
                "[Localization] XML 파일을 찾지 못했습니다.\n" +
                "Expected: Assets/Resources/" +
                xmlResourcePath +
                ".xml"
            );

            loaded = false;
            return;
        }

        try
        {
            XmlDocument document =
                new XmlDocument();

            document.LoadXml(
                xmlFile.text
            );

            // 중요:
            // XML 구조:
            //
            // Localization
            //   Rows
            //      Row
            //
            XmlNodeList rows =
                document.SelectNodes(
                    "/Localization/Rows/Row"
                );

            if (rows == null)
            {
                Debug.LogError(
                    "[Localization] Rows를 찾을 수 없습니다."
                );

                loaded = false;
                return;
            }

            foreach (XmlNode row in rows)
            {
                if (row.Attributes == null)
                    continue;

                XmlAttribute idAttribute =
                    row.Attributes["id"];

                if (idAttribute == null)
                    continue;

                string id =
                    idAttribute.Value;

                if (string.IsNullOrEmpty(id))
                    continue;

                Dictionary<string, string>
                    languageValues =
                        new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase
                        );

                foreach (
                    XmlNode languageNode
                    in row.ChildNodes)
                {
                    if (
                        languageNode.NodeType !=
                        XmlNodeType.Element
                    )
                    {
                        continue;
                    }

                    string languageCode =
                        languageNode.Name;

                    string value =
                        languageNode.InnerText;

                    languageValues[
                        languageCode
                    ] = value;
                }

                localizationData[id] =
                    languageValues;
            }

            loaded = true;

            Debug.Log(
                "[Localization] XML Loaded. Rows = " +
                localizationData.Count
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Localization] XML Load Failed:\n" +
                e
            );

            loaded = false;
        }
    }


    // =====================================================
    // CHANGE LANGUAGE
    // =====================================================

    public void SetLanguage(
        string language)
    {
        if (string.IsNullOrEmpty(language))
            language = "ko";

        CurrentLanguage =
            language;

        Debug.Log(
            "[Localization] Change language => " +
            CurrentLanguage
        );

        if (saveLanguage)
        {
            PlayerPrefs.SetString(
                playerPrefsKey,
                CurrentLanguage
            );

            PlayerPrefs.Save();
        }

        ApplyAll();
    }


    // =====================================================
    // GET TEXT
    // =====================================================

    public string GetText(
        string id,
        string koreanFallback = "")
    {
        if (!loaded)
        {
            Debug.LogWarning(
                "[Localization] Localization data is not loaded."
            );

            return koreanFallback;
        }

        if (string.IsNullOrEmpty(id))
            return koreanFallback;

        if (!localizationData.TryGetValue(
                id,
                out Dictionary<string, string> languages))
        {
            return koreanFallback;
        }

        // 선택한 언어
        if (languages.TryGetValue(
                CurrentLanguage,
                out string translated))
        {
            if (!string.IsNullOrEmpty(translated))
            {
                return translated;
            }
        }

        // 번역이 없으면 한국어
        if (languages.TryGetValue(
                "ko",
                out string korean))
        {
            if (!string.IsNullOrEmpty(korean))
            {
                return korean;
            }
        }

        return koreanFallback;
    }


    // =====================================================
    // APPLY
    // =====================================================

    public void ApplyTo(
        LocalizedUIText localizedText)
    {
        if (localizedText == null)
            return;

        if (localizedText.ignoreLocalization)
            return;

        string result =
            GetText(
                localizedText.Id,
                localizedText.KoreanText
            );

        localizedText.SetText(
            result
        );
    }


    public void ApplyAll()
    {
        if (!loaded)
        {
            Debug.LogWarning(
                "[Localization] ApplyAll failed: XML not loaded."
            );

            return;
        }

        LocalizedUIText[] texts =
            FindObjectsOfType<LocalizedUIText>(
                true
            );

        Debug.Log(
            "[Localization] Applying '" +
            CurrentLanguage +
            "' to " +
            texts.Length +
            " UI texts."
        );

        foreach (
            LocalizedUIText localizedText
            in texts)
        {
            ApplyTo(
                localizedText
            );
        }
    }


    // 동적으로 생성된 UI용
    public static void TryApply(
        LocalizedUIText localizedText)
    {
        if (Instance == null)
            return;

        Instance.ApplyTo(
            localizedText
        );
    }
}