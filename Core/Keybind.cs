using System.Collections.Generic;
using Newtonsoft.Json;

namespace GtavModManager.Core
{
    public class Keybind
    {
        [JsonProperty("configFile")]
        public string ConfigFile { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("modifiers")]
        public List<string> Modifiers { get; set; } = new List<string>();

        [JsonProperty("format")]
        public ConfigFormat Format { get; set; }

        [JsonProperty("rawValue")]
        public string RawValue { get; set; }

        [JsonProperty("isAmbiguous")]
        public bool IsAmbiguous { get; set; }
    }
}
