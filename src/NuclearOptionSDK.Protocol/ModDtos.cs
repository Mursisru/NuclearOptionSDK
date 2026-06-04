namespace NuclearOptionSDK.Protocol;

public sealed class AudioEventPayload
{
    public string clipName { get; set; } = string.Empty;
    public string sourcePath { get; set; } = string.Empty;
    public float volume { get; set; }
}

public sealed class HarmonyGenerateRequest
{
    public string targetType { get; set; } = string.Empty;
    public string methodName { get; set; } = string.Empty;
    public string patchKind { get; set; } = "Prefix";
    public string modNamespace { get; set; } = "MyMod";
    public string className { get; set; } = "MyPatch";
}

public sealed class HarmonyGenerateResponse
{
    public string sourceCode { get; set; } = string.Empty;
}

public sealed class ModBuildRequest
{
    public string modName { get; set; } = "MyMod";
    public string pluginGuid { get; set; } = "com.example.mymod";
    public string outputDirectory { get; set; } = string.Empty;
    public string[] extraSourceFiles { get; set; } = Array.Empty<string>();
    public string? pluginSourceOverride { get; set; }
    public string? csprojOverride { get; set; }
}

public sealed class ModBuildResponse
{
    public bool success { get; set; }
    public string outputPath { get; set; } = string.Empty;
    public string? error { get; set; }
    public string buildLog { get; set; } = string.Empty;
}
