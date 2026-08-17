#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LocalizationExtractor : EditorWindow
{
    private const string OutputDirectory =
        "Assets/Resources/Localization";

    private const string OutputPath =
        OutputDirectory + "/Localization.xml";

    private static readonly string[] DefaultLanguages =
    {
        "ko",
        "en",
        "ja"
    };

    private class LocalizationRow
    {
        public string id;
        public string source;
        public string path;
        public string type;

        public Dictionary<string, string> languages =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );
    }

    [MenuItem("Tools/Localization/Localization Extractor")]
    public static void OpenWindow()
    {
        LocalizationExtractor window =
            GetWindow<LocalizationExtractor>();

        window.titleContent =
            new GUIContent("Localization");

        window.minSize =
            new Vector2(450, 210);
    }

    [MenuItem("Tools/Localization/Extract Current Scene")]
    public static void ExtractMenu()
    {
        ExtractCurrentScene();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Current Scene Localization",
            EditorStyles.boldLabel
        );

        GUILayout.Space(5);

        Scene scene =
            SceneManager.GetActiveScene();

        EditorGUILayout.LabelField(
            "Current Scene:",
            scene.name
        );

        EditorGUILayout.LabelField(
            "Scene Path:",
            scene.path
        );

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "현재 열려 있는 Scene의 UI Text와 TextMeshPro만 추출합니다.\n\n" +
            "기존 Localization.xml의 다른 Scene 데이터와 번역은 유지됩니다.\n" +
            "기본 언어는 한국어(ko)입니다.",
            MessageType.Info
        );

        GUILayout.Space(10);

        if (GUILayout.Button(
                "Extract Current Scene Text",
                GUILayout.Height(50)))
        {
            ExtractCurrentScene();
        }
    }

    public static void ExtractCurrentScene()
    {
        Scene scene =
            SceneManager.GetActiveScene();

        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Localization",
                "활성화된 Scene이 없습니다.",
                "OK"
            );

            return;
        }

        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Localization",
                "먼저 Scene을 저장해주세요.",
                "OK"
            );

            return;
        }

        // 현재 Scene 변경사항 저장
        if (scene.isDirty)
        {
            if (!EditorSceneManager.SaveScene(scene))
                return;
        }

        Directory.CreateDirectory(
            OutputDirectory
        );

        // =========================================
        // 기존 데이터 읽기
        // =========================================

        Dictionary<string, LocalizationRow> allRows;

        List<string> languages;

        LoadExisting(
            out allRows,
            out languages
        );

        int addedCount = 0;
        int updatedCount = 0;

        // =========================================
        // 현재 Scene만 검색
        // =========================================

        foreach (
            GameObject root
            in scene.GetRootGameObjects())
        {
            ProcessHierarchy(
                root,
                scene.path,
                allRows,
                ref addedCount,
                ref updatedCount
            );
        }

        // Scene에 LocalizedUIText가 새로 추가됐으므로 저장
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // =========================================
        // XML 저장
        // =========================================

        WriteXml(
            allRows,
            languages
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[Localization] Current Scene extraction complete.\n" +
            "Scene: " + scene.name + "\n" +
            "Added: " + addedCount + "\n" +
            "Updated: " + updatedCount
        );

        EditorUtility.DisplayDialog(
            "Localization",
            "현재 Scene 추출 완료\n\n" +
            "Scene: " + scene.name + "\n" +
            "새로 추가: " + addedCount + "\n" +
            "업데이트: " + updatedCount,
            "OK"
        );
    }

    private static void ProcessHierarchy(
        GameObject root,
        string scenePath,
        Dictionary<string, LocalizationRow> allRows,
        ref int addedCount,
        ref int updatedCount)
    {
        HashSet<GameObject> targets =
            new HashSet<GameObject>();

        // =========================================
        // Legacy UI.Text
        // =========================================

        Text[] legacyTexts =
            root.GetComponentsInChildren<Text>(true);

        foreach (Text text in legacyTexts)
        {
            if (text != null)
                targets.Add(text.gameObject);
        }

        // =========================================
        // TextMeshPro
        // =========================================

        TMP_Text[] tmpTexts =
            root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in tmpTexts)
        {
            if (text != null)
                targets.Add(text.gameObject);
        }

        // =========================================
        // 각각 처리
        // =========================================

        foreach (GameObject obj in targets)
        {
            if (ShouldIgnoreObject(obj))
                continue;

            string currentText =
                GetText(obj);

            // 빈 텍스트 제외
            if (string.IsNullOrWhiteSpace(
                    currentText))
            {
                continue;
            }

            // Zero Width Space 제거 후 비어있으면 제외
            string cleanText =
                currentText.Replace(
                    "\u200B",
                    ""
                );

            if (string.IsNullOrWhiteSpace(
                    cleanText))
            {
                continue;
            }

            LocalizedUIText localized =
                obj.GetComponent<LocalizedUIText>();

            if (localized == null)
            {
                localized =
                    obj.AddComponent<LocalizedUIText>();
            }

            if (localized.ignoreLocalization)
                continue;

            localized.EditorEnsureId();

            string id =
                localized.Id;

            bool existed =
                allRows.TryGetValue(
                    id,
                    out LocalizationRow row
                );

            if (!existed)
            {
                row =
                    new LocalizationRow();

                row.id = id;

                allRows[id] =
                    row;

                addedCount++;
            }
            else
            {
                updatedCount++;
            }

            // =========================================
            // 한국어 처리
            // =========================================

            /*
             * 기존에 번역 작업을 한 뒤 다시 추출했을 때,
             * 현재 Scene이 영어 등 다른 언어로 표시되어 있으면
             * ko가 덮어써지는 문제가 생길 수 있으므로,
             *
             * LocalizedUIText의 저장된 KoreanText를 우선 사용합니다.
             */

            string koreanText =
                localized.KoreanText;

            if (string.IsNullOrEmpty(koreanText))
            {
                localized.EditorCaptureKorean();

                koreanText =
                    localized.KoreanText;
            }

            // 새 행이거나 ko가 없으면 한국어 저장
            if (!row.languages.ContainsKey("ko") ||
                string.IsNullOrEmpty(row.languages["ko"]))
            {
                row.languages["ko"] =
                    koreanText;
            }

            // =========================================
            // Metadata 업데이트
            // =========================================

            row.source =
                "Scene";

            row.path =
                scenePath +
                " :: " +
                GetHierarchyPath(
                    obj.transform
                );

            TMP_Text tmp =
                obj.GetComponent<TMP_Text>();

            if (tmp != null)
            {
                row.type =
                    tmp.GetType().Name;
            }
            else
            {
                row.type =
                    "UnityEngine.UI.Text";
            }

            EditorUtility.SetDirty(
                localized
            );
        }
    }

    private static string GetText(
        GameObject obj)
    {
        TMP_Text tmp =
            obj.GetComponent<TMP_Text>();

        if (tmp != null)
            return tmp.text;

        Text legacy =
            obj.GetComponent<Text>();

        if (legacy != null)
            return legacy.text;

        return "";
    }

    private static bool ShouldIgnoreObject(
        GameObject obj)
    {
        // =========================================
        // TMP InputField
        // =========================================

        TMP_InputField tmpInput =
            obj.GetComponentInParent<TMP_InputField>(
                true
            );

        if (tmpInput != null)
        {
            // Placeholder는 번역 대상
            if (tmpInput.placeholder != null &&
                tmpInput.placeholder.gameObject == obj)
            {
                return false;
            }

            // 실제 입력 Text는 제외
            if (tmpInput.textComponent != null &&
                tmpInput.textComponent.gameObject == obj)
            {
                return true;
            }
        }

        // =========================================
        // Legacy InputField
        // =========================================

        InputField legacyInput =
            obj.GetComponentInParent<InputField>(
                true
            );

        if (legacyInput != null)
        {
            // Placeholder는 번역 가능
            if (legacyInput.placeholder != null &&
                legacyInput.placeholder.gameObject == obj)
            {
                return false;
            }

            // 사용자 입력 Text 제외
            if (legacyInput.textComponent != null &&
                legacyInput.textComponent.gameObject == obj)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetHierarchyPath(
        Transform target)
    {
        List<string> names =
            new List<string>();

        Transform current =
            target;

        while (current != null)
        {
            names.Add(
                current.name
            );

            current =
                current.parent;
        }

        names.Reverse();

        return string.Join(
            "/",
            names
        );
    }

    // =========================================================
    // 기존 XML 불러오기
    // =========================================================

    private static void LoadExisting(
        out Dictionary<string, LocalizationRow> rows,
        out List<string> languages)
    {
        rows =
            new Dictionary<string, LocalizationRow>();

        languages =
            new List<string>();

        if (!File.Exists(OutputPath))
        {
            languages.AddRange(
                DefaultLanguages
            );

            return;
        }

        try
        {
            XmlDocument document =
                new XmlDocument();

            document.Load(
                OutputPath
            );

            // =========================================
            // 언어 목록
            // =========================================

            XmlNode languageNode =
                document.SelectSingleNode(
                    "//Localization/Languages"
                );

            if (languageNode != null)
            {
                foreach (
                    XmlNode node
                    in languageNode.ChildNodes)
                {
                    if (node.Name != "Language")
                        continue;

                    string language =
                        node.Attributes?["code"]?.Value;

                    if (!string.IsNullOrEmpty(
                            language))
                    {
                        languages.Add(
                            language
                        );
                    }
                }
            }

            foreach (
                string defaultLanguage
                in DefaultLanguages)
            {
                if (!languages.Any(
                        x =>
                            string.Equals(
                                x,
                                defaultLanguage,
                                StringComparison.OrdinalIgnoreCase
                            )))
                {
                    languages.Add(
                        defaultLanguage
                    );
                }
            }

            // =========================================
            // 기존 행
            // =========================================

            XmlNodeList rowNodes =
                document.SelectNodes(
                    "//Localization/Rows/Row"
                );

            if (rowNodes != null)
            {
                foreach (
                    XmlNode node
                    in rowNodes)
                {
                    string id =
                        node.Attributes?["id"]?.Value;

                    if (string.IsNullOrEmpty(id))
                        continue;

                    LocalizationRow row =
                        new LocalizationRow();

                    row.id =
                        id;

                    row.source =
                        node.Attributes?["source"]
                            ?.Value ?? "";

                    row.path =
                        node.Attributes?["path"]
                            ?.Value ?? "";

                    row.type =
                        node.Attributes?["type"]
                            ?.Value ?? "";

                    foreach (
                        XmlNode child
                        in node.ChildNodes)
                    {
                        row.languages[
                            child.Name
                        ] =
                            child.InnerText;
                    }

                    rows[id] =
                        row;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Localization] Existing XML load error:\n" +
                e
            );

            rows.Clear();

            languages.Clear();

            languages.AddRange(
                DefaultLanguages
            );
        }
    }

    // =========================================================
    // XML 저장
    // =========================================================

    private static void WriteXml(
        Dictionary<string, LocalizationRow> rows,
        List<string> languages)
    {
        // =========================================
        // ko 항상 첫 번째
        // =========================================

        languages.RemoveAll(
            x =>
                string.Equals(
                    x,
                    "ko",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        languages.Insert(
            0,
            "ko"
        );

        XmlWriterSettings settings =
            new XmlWriterSettings
            {
                Indent = true,
                Encoding =
                    new UTF8Encoding(false)
            };

        using (
            XmlWriter writer =
                XmlWriter.Create(
                    OutputPath,
                    settings
                ))
        {
            writer.WriteStartDocument();

            writer.WriteStartElement(
                "Localization"
            );

            // =========================================
            // Languages
            // =========================================

            writer.WriteStartElement(
                "Languages"
            );

            foreach (
                string language
                in languages)
            {
                writer.WriteStartElement(
                    "Language"
                );

                writer.WriteAttributeString(
                    "code",
                    language
                );

                writer.WriteEndElement();
            }

            writer.WriteEndElement();

            // =========================================
            // Rows
            // =========================================

            writer.WriteStartElement(
                "Rows"
            );

            foreach (
                LocalizationRow row
                in rows.Values
                    .OrderBy(
                        x => x.path
                    ))
            {
                writer.WriteStartElement(
                    "Row"
                );

                writer.WriteAttributeString(
                    "id",
                    row.id
                );

                writer.WriteAttributeString(
                    "source",
                    row.source ?? ""
                );

                writer.WriteAttributeString(
                    "path",
                    row.path ?? ""
                );

                writer.WriteAttributeString(
                    "type",
                    row.type ?? ""
                );

                foreach (
                    string language
                    in languages)
                {
                    writer.WriteStartElement(
                        language
                    );

                    if (row.languages.TryGetValue(
                            language,
                            out string value))
                    {
                        writer.WriteString(
                            value ?? ""
                        );
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();

            writer.WriteEndElement();

            writer.WriteEndDocument();
        }
    }
}

#endif