using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MobileModSystem
{
    /// <summary>
    /// 모드팩에 포함되는 사용자 편집용 key=value 텍스트 설정입니다.
    /// 임의 코드를 실행하지 않고 문자열을 안전하게 파싱하는 용도로만 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeModTextConfig : MonoBehaviour
    {
        public const int MaxTextBytes = 256 * 1024;
        public const string DefaultFileName = "mod_settings.txt";

        [SerializeField, TextArea(12, 40)]
        private string textContent;

        [SerializeField]
        private string sourceFilePath;

        public string TextContent => textContent ?? string.Empty;
        public string SourceFilePath => sourceFilePath ?? string.Empty;

        public void EnsureDefault(string modName, string author, string customTemplate = null)
        {
            if (!string.IsNullOrWhiteSpace(textContent))
                return;

            textContent = string.IsNullOrWhiteSpace(customTemplate)
                ? CreateDefaultTemplate(modName, author)
                : ApplyTemplateTokens(customTemplate, modName, author);

            ValidateTextSize(textContent);
            sourceFilePath = string.Empty;
        }

        public void ResetToDefault(string modName, string author, string customTemplate = null)
        {
            textContent = string.IsNullOrWhiteSpace(customTemplate)
                ? CreateDefaultTemplate(modName, author)
                : ApplyTemplateTokens(customTemplate, modName, author);

            ValidateTextSize(textContent);
            sourceFilePath = string.Empty;
        }

        public void SetText(string text)
        {
            text = text ?? string.Empty;
            ValidateTextSize(text);
            textContent = NormalizeNewLines(text);
            sourceFilePath = string.Empty;
        }

        public void LoadFromFile(string path, bool copyIntoWorkspace = true)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("설정 텍스트 파일을 찾을 수 없습니다.", path);

            if (!string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("모드 설정 파일은 .txt만 지원합니다.");

            FileInfo info = new FileInfo(path);
            if (info.Length > MaxTextBytes)
                throw new InvalidDataException("설정 텍스트 파일이 256KB 제한을 초과했습니다.");

            string storedPath = copyIntoWorkspace
                ? ModPathUtility.CopyIntoWorkspace(path, "Config")
                : path;

            string loaded = File.ReadAllText(storedPath, Encoding.UTF8);
            ValidateTextSize(loaded);

            textContent = NormalizeNewLines(loaded);
            sourceFilePath = storedPath;
        }

        public string SaveToWorkspace(string fileName = DefaultFileName)
        {
            string safeName = ModPathUtility.MakeSafeFileName(
                Path.GetFileNameWithoutExtension(fileName),
                "mod_settings") + ".txt";

            string directory = Path.Combine(Application.persistentDataPath, "ModWorkspace", "Config");
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, safeName);
            File.WriteAllText(path, TextContent, new UTF8Encoding(false));
            sourceFilePath = path;
            return path;
        }

        public Dictionary<string, string> GetAllValues()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (StringReader reader = new StringReader(TextContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!TryParseLine(line, out string key, out string value))
                        continue;

                    // 같은 키가 여러 번 나오면 가장 마지막 값을 사용합니다.
                    values[key] = value;
                }
            }

            return values;
        }

        public bool ContainsKey(string key)
        {
            return TryGetString(key, out _);
        }

        public string GetString(string key, string fallback = "")
        {
            return TryGetString(key, out string value) ? value : fallback;
        }

        public bool TryGetString(string key, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            Dictionary<string, string> values = GetAllValues();
            return values.TryGetValue(key.Trim(), out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            value = default;
            return TryGetString(key, out string raw) &&
                   int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetFloat(string key, out float value)
        {
            value = default;
            return TryGetString(key, out string raw) &&
                   float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetBool(string key, out bool value)
        {
            value = default;
            if (!TryGetString(key, out string raw))
                return false;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                case "on":
                    value = true;
                    return true;

                case "false":
                case "0":
                case "no":
                case "off":
                    value = false;
                    return true;

                default:
                    return false;
            }
        }

        public bool TryGetVector3(string key, out Vector3 value)
        {
            value = default;
            if (!TryGetString(key, out string raw))
                return false;

            string[] parts = raw.Split(',');
            if (parts.Length != 3)
                return false;

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                return false;

            value = new Vector3(x, y, z);
            return true;
        }

        /// <summary>
        /// 기존 주석과 순서를 최대한 유지하면서 값을 추가하거나 교체합니다.
        /// </summary>
        public void SetValue(string key, string value)
        {
            key = ValidateKey(key);
            value = SanitizeSingleLineValue(value);

            string[] lines = NormalizeNewLines(TextContent).Split('\n');
            bool replaced = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!TryParseLine(lines[i], out string existingKey, out _))
                    continue;

                if (!string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                lines[i] = key + "=" + value;
                replaced = true;
                break;
            }

            string result = string.Join("\n", lines);
            if (!replaced)
            {
                if (result.Length > 0 && !result.EndsWith("\n", StringComparison.Ordinal))
                    result += "\n";

                result += key + "=" + value + "\n";
            }

            SetText(result);
        }

        public bool RemoveValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string[] lines = NormalizeNewLines(TextContent).Split('\n');
            List<string> remaining = new List<string>(lines.Length);
            bool removed = false;

            foreach (string line in lines)
            {
                if (TryParseLine(line, out string existingKey, out _) &&
                    string.Equals(existingKey, key.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    removed = true;
                    continue;
                }

                remaining.Add(line);
            }

            if (removed)
                SetText(string.Join("\n", remaining));

            return removed;
        }

        public static string CreateDefaultTemplate(string modName, string author)
        {
            return
                "# SDGMOD 사용자 설정 파일\n" +
                "# 형식: key=value\n" +
                "# # 또는 ; 로 시작하는 줄은 주석입니다.\n" +
                "# 숫자의 소수점은 마침표(.)를 사용하고 Vector3는 x,y,z로 작성합니다.\n" +
                "\n" +
                "mod.name=" + SanitizeSingleLineValue(modName) + "\n" +
                "mod.author=" + SanitizeSingleLineValue(author) + "\n" +
                "mod.category=custom\n" +
                "\n" +
                "object.displayName=" + SanitizeSingleLineValue(modName) + "\n" +
                "object.enabled=true\n" +
                "object.uniformScale=1.0\n" +
                "spawn.position=0,0,0\n" +
                "spawn.rotation=0,0,0\n" +
                "\n" +
                "audio.volume=1.0\n" +
                "custom.note=\n";
        }

        private static string ApplyTemplateTokens(string template, string modName, string author)
        {
            return (template ?? string.Empty)
                .Replace("{{MOD_NAME}}", SanitizeSingleLineValue(modName))
                .Replace("{{AUTHOR}}", SanitizeSingleLineValue(author));
        }

        private static bool TryParseLine(string line, out string key, out string value)
        {
            key = null;
            value = null;

            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal) ||
                trimmed.StartsWith(";", StringComparison.Ordinal))
                return false;

            int separator = trimmed.IndexOf('=');
            if (separator <= 0)
                return false;

            string parsedKey = trimmed.Substring(0, separator).Trim();
            if (!IsValidKey(parsedKey))
                return false;

            key = parsedKey;
            value = trimmed.Substring(separator + 1).Trim();
            return true;
        }

        private static string ValidateKey(string key)
        {
            key = key?.Trim();
            if (!IsValidKey(key))
                throw new ArgumentException("설정 키에는 영문자, 숫자, '.', '_', '-'만 사용할 수 있습니다.", nameof(key));

            return key;
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
                return false;

            foreach (char c in key)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')
                    continue;

                return false;
            }

            return true;
        }

        private static string SanitizeSingleLineValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private static string NormalizeNewLines(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }

        private static void ValidateTextSize(string text)
        {
            if (Encoding.UTF8.GetByteCount(text ?? string.Empty) > MaxTextBytes)
                throw new InvalidDataException("설정 텍스트가 256KB 제한을 초과했습니다.");
        }
    }
}
