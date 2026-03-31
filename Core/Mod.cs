using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GtavModManager.Core
{
    public class Mod
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("sourceUrl")]
        public string SourceUrl { get; set; }

        [JsonProperty("installDate")]
        public DateTime InstallDate { get; set; }

        [JsonProperty("status")]
        public ModStatus Status { get; set; }

        [JsonProperty("type")]
        public ModType Type { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        [JsonProperty("loadOrder")]
        public int LoadOrder { get; set; }

        [JsonProperty("files")]
        public List<string> Files { get; set; } = new List<string>();

        [JsonProperty("dependencies")]
        public List<ModDependency> Dependencies { get; set; } = new List<ModDependency>();

        [JsonProperty("keybinds")]
        public List<Keybind> Keybinds { get; set; } = new List<Keybind>();

        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";
    }
}
