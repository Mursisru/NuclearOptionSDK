using System.Text;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.ModKit;

public static class VisualHudModExporter
{
    public static ModBuildRequest CreateBuildRequest(
        string modName,
        string pluginGuid,
        VisualHudLayoutPayload layout,
        string outputDirectory)
    {
        return new ModBuildRequest
        {
            modName = modName,
            pluginGuid = pluginGuid,
            outputDirectory = outputDirectory,
            pluginSourceOverride = GeneratePluginSource(modName, pluginGuid, layout),
            csprojOverride = GenerateCsproj(modName + "_Engine")
        };
    }

    private static string GeneratePluginSource(string modName, string pluginGuid, VisualHudLayoutPayload layout)
    {
        var labels = new StringBuilder();
        foreach (var label in layout.labels ?? Array.Empty<OverlayLabel>())
        {
            labels.AppendLine($@"            new LabelDef {{ text = ""{Escape(label.text)}"", x = {label.x}f, y = {label.y}f, fontSize = {label.fontSize}f, colorHtml = ""{Escape(label.colorHtml)}"", visible = {(label.visible ? "true" : "false")} }},");
        }

        var prims = new StringBuilder();
        foreach (var prim in layout.primitives ?? Array.Empty<OverlayPrimitive>())
        {
            prims.AppendLine($@"            new PrimDef {{ kind = ""{Escape(prim.kind)}"", x1 = {prim.x1}f, y1 = {prim.y1}f, x2 = {prim.x2}f, y2 = {prim.y2}f, radius = {prim.radius}f, colorHtml = ""{Escape(prim.colorHtml)}"" }},");
        }

        return $@"using BepInEx;
using UnityEngine;

namespace {modName}_Engine;

[BepInPlugin(""{pluginGuid}"", ""{modName}"", ""1.0.0"")]
public sealed class {modName}Plugin : BaseUnityPlugin
{{
    private void Awake()
    {{
        var go = new GameObject(""{modName}_VisualHud"");
        DontDestroyOnLoad(go);
        go.AddComponent<VisualHudRuntime>();
        Logger.LogInfo(""{modName} visual HUD mod loaded."");
    }}
}}

public sealed class VisualHudRuntime : MonoBehaviour
{{
    private static readonly LabelDef[] Labels =
    {{
{labels}    }};

    private static readonly PrimDef[] Primitives =
    {{
{prims}    }};

    private GUIStyle? _labelStyle;

    private void OnGUI()
    {{
        foreach (var p in Primitives)
        {{
            if (!ColorUtility.TryParseHtmlString(p.colorHtml, out var color)) continue;
            DrawPrimitive(p, color);
        }}

        foreach (var label in Labels)
        {{
            if (!label.visible || string.IsNullOrEmpty(label.text)) continue;
            if (!ColorUtility.TryParseHtmlString(label.colorHtml, out var color)) continue;
            _labelStyle ??= new GUIStyle(GUI.skin.label) {{ richText = false }};
            _labelStyle.fontSize = Mathf.RoundToInt(label.fontSize);
            _labelStyle.normal.textColor = color;
            GUI.Label(new Rect(label.x, label.y, 800f, 40f), label.text, _labelStyle);
        }}
    }}

    private static void DrawPrimitive(PrimDef p, Color color)
    {{
        var prev = GUI.color;
        GUI.color = color;
        var tex = Texture2D.whiteTexture;
        if (p.kind == ""circle"")
        {{
            var size = p.radius * 2f;
            GUI.DrawTexture(new Rect(p.x1 - p.radius, p.y1 - p.radius, size, size), tex);
        }}
        else
        {{
            var minX = Mathf.Min(p.x1, p.x2);
            var minY = Mathf.Min(p.y1, p.y2);
            var w = Mathf.Max(Mathf.Abs(p.x2 - p.x1), 2f);
            var h = Mathf.Max(Mathf.Abs(p.y2 - p.y1), 2f);
            GUI.DrawTexture(new Rect(minX, minY, w, h), tex);
        }}

        GUI.color = prev;
    }}

    private sealed class LabelDef
    {{
        public string text = """";
        public float x;
        public float y;
        public float fontSize = 18f;
        public string colorHtml = ""#FFFFFF"";
        public bool visible = true;
    }}

    private sealed class PrimDef
    {{
        public string kind = ""line"";
        public float x1;
        public float y1;
        public float x2;
        public float y2;
        public float radius = 40f;
        public string colorHtml = ""#FF0000"";
    }}
}}
";
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string GenerateCsproj(string assemblyName)
    {
        return $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>{assemblyName}</AssemblyName>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""BepInEx"">
      <HintPath>$(NuclearOptionRoot)\BepInEx\core\BepInEx.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include=""UnityEngine"">
      <HintPath>$(NuclearOptionRoot)\NuclearOption_Data\Managed\UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include=""UnityEngine.CoreModule"">
      <HintPath>$(NuclearOptionRoot)\NuclearOption_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include=""UnityEngine.IMGUIModule"">
      <HintPath>$(NuclearOptionRoot)\NuclearOption_Data\Managed\UnityEngine.IMGUIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>";
    }
}
