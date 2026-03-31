using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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

        public bool IsConfigFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
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

            try
            {
                var doc = XDocument.Load(filePath);
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
