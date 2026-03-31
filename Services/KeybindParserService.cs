using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GtavModManager.Core;

namespace GtavModManager.Services
{
    public class KeybindParserService
    {
        // Patterns that look like GTA V / LSPDFR keybinds
        private static readonly HashSet<string> KnownModifiers =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Ctrl", "Control", "Shift", "Alt", "Win" };

        private static readonly Regex KeyPattern = new Regex(
            @"^((?:Ctrl|Control|Shift|Alt|Win)\+)*([A-Z]|F\d{1,2}|Numpad\d|OemMinus|OemPlus|PageUp|PageDown|Home|End|Insert|Delete|Back|Space|Tab|Enter|Escape|Up|Down|Left|Right|\d)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Issue #9: filenames that are never keybind config files
        private static readonly HashSet<string> SkipFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "stats.xml", "changelog.xml", "changes.xml", "readme.xml", "readme.txt",
            "license.xml", "credits.xml", "version.xml"
        };

        // Issue #7: INI sections that may contain keybinds
        private static readonly HashSet<string> KeybindSectionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "keys", "keybinds", "keybinding", "keybindings", "controls", "hotkeys",
            "shortcuts", "bindings", "input", "keyboard"
        };

        // Issue #7: folder names that indicate translation/locale files
        private static readonly HashSet<string> TranslationFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "translations", "localization", "locale", "locales", "lang", "language",
            "languages", "strings", "i18n", "l10n"
        };

        // Issue #7: XML root element names that indicate translation/data files
        private static readonly HashSet<string> TranslationRootElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Translations", "Languages", "Strings", "Language", "Localization",
            "StringTable", "Locale", "DialogTable", "Dialogue"
        };

        public bool IsConfigFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            // Issue #9: skip known non-config filenames
            string filename = Path.GetFileName(filePath);
            if (SkipFilenames.Contains(filename)) return false;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".ini" || ext == ".xml" || ext == ".json";
        }

        public List<Keybind> ScanModFiles(List<string> filePaths)
        {
            var result = new List<Keybind>();
            if (filePaths == null) return result;

            foreach (var path in filePaths.Where(IsConfigFile))
            {
                try
                {
                    result.AddRange(ParseConfigFile(path));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[KeybindParser] Error parsing {path}: {ex.Message}");
                }
            }
            return result;
        }

        public List<Keybind> ParseConfigFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".ini": return ParseIniFile(filePath);
                case ".xml": return ParseXmlFile(filePath);
                case ".json": return ParseJsonFile(filePath);
                default: return new List<Keybind>();
            }
        }

        public List<Keybind> ParseIniFile(string filePath)
        {
            var result = new List<Keybind>();
            if (!File.Exists(filePath)) return result;

            string currentSection = "";
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                string line = rawLine.Trim();
                if (line.StartsWith(";") || line.StartsWith("#") || string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                // Issue #7: only parse keybind-relevant sections (or top-level before any section)
                if (!IsKeybindSection(currentSection)) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                // Strip inline comments
                int comment = value.IndexOf(';');
                if (comment >= 0) value = value.Substring(0, comment).Trim();

                if (LooksLikeKeybind(value, out string parsedKey, out List<string> modifiers))
                {
                    result.Add(new Keybind
                    {
                        ConfigFile = filePath,
                        Action = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}/{key}",
                        IniSection = currentSection,   // Issue #6: track section for write-back
                        Key = parsedKey,
                        Modifiers = modifiers,
                        Format = ConfigFormat.Ini,
                        RawValue = value
                    });
                }
            }
            return result;
        }

        public List<Keybind> ParseXmlFile(string filePath)
        {
            var result = new List<Keybind>();
            if (!File.Exists(filePath)) return result;

            // Issue #7: skip files in translation/locale folders
            if (IsTranslationFile(filePath)) return result;

            try
            {
                var doc = XDocument.Load(filePath);

                // Issue #7: skip translation-rooted documents
                string rootName = doc.Root?.Name.LocalName ?? "";
                if (TranslationRootElements.Contains(rootName)) return result;

                foreach (var element in doc.Descendants())
                {
                    // Check element text
                    string text = element.Value?.Trim();
                    if (!string.IsNullOrEmpty(text) && !element.HasElements
                        && LooksLikeKeybind(text, out string pk, out List<string> mods))
                    {
                        result.Add(new Keybind
                        {
                            ConfigFile = filePath,
                            Action = element.Name.LocalName,
                            Key = pk,
                            Modifiers = mods,
                            Format = ConfigFormat.Xml,
                            RawValue = text
                        });
                    }

                    // Check attributes
                    foreach (var attr in element.Attributes())
                    {
                        if (LooksLikeKeybind(attr.Value, out string apk, out List<string> amods))
                        {
                            result.Add(new Keybind
                            {
                                ConfigFile = filePath,
                                Action = $"{element.Name.LocalName}/@{attr.Name.LocalName}",
                                Key = apk,
                                Modifiers = amods,
                                Format = ConfigFormat.Xml,
                                RawValue = attr.Value
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KeybindParser] XML parse error {filePath}: {ex.Message}");
            }
            return result;
        }

        public List<Keybind> ParseJsonFile(string filePath)
        {
            var result = new List<Keybind>();
            if (!File.Exists(filePath)) return result;

            try
            {
                string json = File.ReadAllText(filePath);
                var obj = JToken.Parse(json);
                WalkJson(obj, "", filePath, result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KeybindParser] JSON parse error {filePath}: {ex.Message}");
            }
            return result;
        }

        private void WalkJson(JToken token, string path, string filePath, List<Keybind> result)
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                    WalkJson(prop.Value, string.IsNullOrEmpty(path) ? prop.Name : $"{path}/{prop.Name}", filePath, result);
            }
            else if (token is JArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                    WalkJson(arr[i], $"{path}[{i}]", filePath, result);
            }
            else if (token.Type == JTokenType.String)
            {
                string value = token.Value<string>();
                if (LooksLikeKeybind(value, out string pk, out List<string> mods))
                {
                    result.Add(new Keybind
                    {
                        ConfigFile = filePath,
                        Action = path,
                        Key = pk,
                        Modifiers = mods,
                        Format = ConfigFormat.Json,
                        RawValue = value
                    });
                }
            }
        }

        // Issue #6: Write-back — update a keybind value in its config file
        public bool WriteKeybindValue(Keybind keybind, string newValue, string filePath)
        {
            if (keybind == null || string.IsNullOrWhiteSpace(newValue)) return false;
            switch (keybind.Format)
            {
                case ConfigFormat.Ini:  return WriteIniKeybindValue(keybind, newValue, filePath);
                case ConfigFormat.Xml:  return WriteXmlKeybindValue(keybind, newValue, filePath);
                case ConfigFormat.Json: return WriteJsonKeybindValue(keybind, newValue, filePath);
                default: return false;
            }
        }

        private bool WriteIniKeybindValue(Keybind keybind, string newValue, string filePath)
        {
            if (!File.Exists(filePath)) return false;

            string targetSection = keybind.IniSection ?? "";
            // Action is "Section/Key" or just "Key" for top-level
            string targetKey = keybind.Action;
            if (!string.IsNullOrEmpty(targetSection) && targetKey.StartsWith(targetSection + "/", StringComparison.OrdinalIgnoreCase))
                targetKey = targetKey.Substring(targetSection.Length + 1);

            var lines = File.ReadAllLines(filePath);
            string currentSection = "";
            bool inTargetSection = string.IsNullOrEmpty(targetSection);

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    inTargetSection = string.Equals(currentSection, targetSection, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inTargetSection) continue;

                int eq = lines[i].IndexOf('=');
                if (eq < 0) continue;

                string key = lines[i].Substring(0, eq).Trim();
                if (!string.Equals(key, targetKey, StringComparison.OrdinalIgnoreCase)) continue;

                // Preserve inline comment if present
                string afterEq = lines[i].Substring(eq + 1);
                int commentPos = afterEq.IndexOf(';');
                string trailingComment = commentPos >= 0
                    ? " " + afterEq.Substring(commentPos).TrimStart()
                    : "";

                lines[i] = lines[i].Substring(0, eq + 1) + newValue + trailingComment;
                File.WriteAllLines(filePath, lines);
                return true;
            }
            return false;
        }

        private bool WriteXmlKeybindValue(Keybind keybind, string newValue, string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                var doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
                string action = keybind.Action;

                if (action.Contains("/@"))
                {
                    // Attribute: "ElementName/@AttributeName"
                    int atPos = action.IndexOf("/@");
                    string elemName = action.Substring(0, atPos);
                    string attrName = action.Substring(atPos + 2);

                    var elem = doc.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == elemName
                            && e.Attribute(attrName) != null
                            && e.Attribute(attrName).Value == keybind.RawValue);
                    if (elem == null) return false;
                    elem.Attribute(attrName).Value = newValue;
                }
                else
                {
                    // Element text: "ElementName"
                    var elem = doc.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == action
                            && !e.HasElements
                            && e.Value == keybind.RawValue);
                    if (elem == null) return false;
                    elem.Value = newValue;
                }

                doc.Save(filePath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KeybindParser] XML write error {filePath}: {ex.Message}");
                return false;
            }
        }

        private bool WriteJsonKeybindValue(Keybind keybind, string newValue, string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                string json = File.ReadAllText(filePath);
                var root = JToken.Parse(json);

                string[] parts = keybind.Action.Split('/');
                JToken current = root;

                // Navigate to parent
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    current = NavigateJsonToken(current, parts[i]);
                    if (current == null) return false;
                }

                // Set value on last segment
                string lastPart = parts[parts.Length - 1];
                int arrayIdx = ParseArrayIndex(ref lastPart);

                if (arrayIdx >= 0 && current is JObject objForArray)
                    current = objForArray[lastPart];

                if (arrayIdx >= 0 && current is JArray arr)
                    arr[arrayIdx] = newValue;
                else if (current is JObject obj && obj[lastPart] != null)
                    obj[lastPart] = newValue;
                else
                    return false;

                File.WriteAllText(filePath, root.ToString(Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KeybindParser] JSON write error {filePath}: {ex.Message}");
                return false;
            }
        }

        private static JToken NavigateJsonToken(JToken token, string part)
        {
            int arrayIdx = ParseArrayIndex(ref part);
            JToken next = token is JObject obj ? obj[part] : null;
            if (next == null) return null;
            if (arrayIdx >= 0 && next is JArray arr) return arr[arrayIdx];
            return next;
        }

        private static int ParseArrayIndex(ref string part)
        {
            if (!part.EndsWith("]")) return -1;
            int open = part.LastIndexOf('[');
            if (open < 0) return -1;
            if (int.TryParse(part.Substring(open + 1, part.Length - open - 2), out int idx))
            {
                part = part.Substring(0, open);
                return idx;
            }
            return -1;
        }

        // Issue #7: true if the INI section is likely to contain keybinds
        private static bool IsKeybindSection(string sectionName)
        {
            if (string.IsNullOrEmpty(sectionName)) return true; // top-level (no section) — include
            return KeybindSectionKeywords.Any(kw =>
                sectionName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Issue #7: true if the file path suggests it is a translation/locale file
        private static bool IsTranslationFile(string filePath)
        {
            string normalized = filePath.Replace('\\', '/');
            string[] segments = normalized.Split('/');
            // Check all directory segments (not the filename itself)
            foreach (string seg in segments.Take(segments.Length - 1))
            {
                if (TranslationFolderNames.Contains(seg)) return true;
            }
            // Check filename keywords
            string nameOnly = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            return nameOnly.Contains("translation") || nameOnly.Contains("locale")
                || nameOnly.Contains("localization") || nameOnly.Contains("_lang");
        }

        private bool LooksLikeKeybind(string value, out string key, out List<string> modifiers)
        {
            key = null;
            modifiers = new List<string>();

            if (string.IsNullOrWhiteSpace(value)) return false;

            var match = KeyPattern.Match(value.Trim());
            if (!match.Success) return false;

            string[] parts = value.Trim().Split('+');
            key = parts[parts.Length - 1].Trim();
            modifiers = parts.Length > 1
                ? parts.Take(parts.Length - 1).Select(m => m.Trim()).ToList()
                : new List<string>();

            return true;
        }
    }
}
