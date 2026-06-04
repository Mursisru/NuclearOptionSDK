using System.Text;
using Newtonsoft.Json;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.ModKit;

public static class LogicModExporter
{
    public static ModBuildRequest CreateBuildRequest(
        string modName,
        string pluginGuid,
        LogicProject project,
        string outputDirectory,
        string? pluginSourceOverride = null)
    {
        return new ModBuildRequest
        {
            modName = modName,
            pluginGuid = pluginGuid,
            outputDirectory = outputDirectory,
            pluginSourceOverride = pluginSourceOverride ?? GeneratePluginSource(modName, pluginGuid, project),
            csprojOverride = null
        };
    }

    private static string GeneratePluginSource(string modName, string pluginGuid, LogicProject project)
    {
        var json = JsonConvert.SerializeObject(project);
        var escaped = Escape(json);
        return $@"using BepInEx;
using Newtonsoft.Json;
using NuclearOptionSDK.LogicCore;
using NuclearOptionSDK.Protocol;
using UnityEngine;

namespace {modName}_Engine;

[BepInPlugin(""{pluginGuid}"", ""{modName}"", ""1.0.0"")]
public sealed class {modName}Plugin : BaseUnityPlugin
{{
    private LogicRuntimeHost? _host;

    private void Awake()
    {{
        var go = new GameObject(""{modName}_Logic"");
        DontDestroyOnLoad(go);
        _host = go.AddComponent<LogicRuntimeHost>();
        _host.Initialize(@""{escaped}"");
        Logger.LogInfo(""{modName} logic mod loaded."");
    }}
}}

public sealed class LogicRuntimeHost : MonoBehaviour
{{
    private LogicGraphEvaluator _evaluator = new();
    private LogicStateStore _state = new();
    private LogicProject _project = new();
    private float _timer;

    public void Initialize(string json)
    {{
        _project = JsonConvert.DeserializeObject<LogicProject>(json) ?? new LogicProject();
    }}

    private void Update()
    {{
        _state.Tick(Time.unscaledDeltaTime);
        _timer += Time.unscaledDeltaTime;
        if (_timer < 0.1f) return;
        _timer = 0f;
        // Standalone mod: telemetry stub — embed values via generated bindings in full export.
    }}
}}";
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
