using System.Collections.Generic;

namespace GtavModManager.Core
{
    /// <summary>
    /// Catalog of known GTA V framework and utility files.
    /// These are not mods themselves but are dependencies that mods rely on.
    /// </summary>
    public class KnownFramework
    {
        public string FileName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Mod types that implicitly depend on this framework when it is present.
        /// </summary>
        public List<ModType> RequiredBy { get; set; } = new List<ModType>();
    }

    public static class KnownFrameworks
    {
        /// <summary>
        /// Well-known framework files keyed by filename (case-insensitive).
        /// </summary>
        public static readonly Dictionary<string, KnownFramework> All =
            new Dictionary<string, KnownFramework>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ScriptHookV.dll"] = new KnownFramework
            {
                FileName = "ScriptHookV.dll",
                DisplayName = "Script Hook V",
                Description = "Core native scripting framework. Required by all ASI plugins.",
                RequiredBy = new List<ModType> { ModType.AsiPlugin }
            },
            ["dinput8.dll"] = new KnownFramework
            {
                FileName = "dinput8.dll",
                DisplayName = "ASI Loader",
                Description = "Loads ASI plugins on startup. Bundled with Script Hook V.",
                RequiredBy = new List<ModType> { ModType.AsiPlugin }
            },
            ["ScriptHookVDotNet.dll"] = new KnownFramework
            {
                FileName = "ScriptHookVDotNet.dll",
                DisplayName = "Script Hook V .NET",
                Description = "Managed scripting layer for .NET/C# scripts. Required by SHVDN scripts.",
                RequiredBy = new List<ModType> { ModType.ShvdnScript }
            },
            ["ScriptHookVDotNet2.dll"] = new KnownFramework
            {
                FileName = "ScriptHookVDotNet2.dll",
                DisplayName = "Script Hook V .NET 2",
                Description = "Script Hook V .NET v2 (legacy). Required by some older SHVDN scripts.",
                RequiredBy = new List<ModType> { ModType.ShvdnScript }
            },
            ["ScriptHookVDotNet3.dll"] = new KnownFramework
            {
                FileName = "ScriptHookVDotNet3.dll",
                DisplayName = "Script Hook V .NET 3",
                Description = "Script Hook V .NET v3. Required by newer SHVDN scripts.",
                RequiredBy = new List<ModType> { ModType.ShvdnScript }
            },
            ["RagePluginHook.dll"] = new KnownFramework
            {
                FileName = "RagePluginHook.dll",
                DisplayName = "RAGEPluginHook",
                Description = "Plugin hook for RAGE engine. Required by all RPH plugins.",
                RequiredBy = new List<ModType> { ModType.RagePlugin, ModType.LspdfPlugin }
            },
            ["RAGENativeUI.dll"] = new KnownFramework
            {
                FileName = "RAGENativeUI.dll",
                DisplayName = "RAGENativeUI",
                Description = "UI menu library for RPH plugins. Common shared dependency.",
                RequiredBy = new List<ModType> { ModType.RagePlugin, ModType.LspdfPlugin }
            },
            ["NativeUI.dll"] = new KnownFramework
            {
                FileName = "NativeUI.dll",
                DisplayName = "NativeUI",
                Description = "UI menu library for SHVDN scripts. Common shared dependency.",
                RequiredBy = new List<ModType> { ModType.ShvdnScript }
            },
        };
    }
}
