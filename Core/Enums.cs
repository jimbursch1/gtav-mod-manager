namespace GtavModManager.Core
{
    public enum ModStatus { Enabled, Disabled, Unknown }

    public enum ModType
    {
        AsiPlugin,       // .asi files loaded by ScriptHookV
        ShvdnScript,     // .dll/.cs in scripts/ loaded by ScriptHookVDotNet
        OpenIVPackage,   // OIV texture/model replacement packages
        ElsConfig,       // Emergency Lighting System configs
        LspdfPlugin,     // .dll in plugins/LSPDFR/
        RagePlugin,      // .dll in plugins/ (RagePluginHook)
        StandaloneTool,  // External executables (ENB, etc.)
        Other
    }

    public enum ConflictSeverity { Info, Warning, Error }

    public enum OperationState { Idle, Running, Success, Failed }

    public enum ConfigFormat { Ini, Xml, Json, Unknown }
}
