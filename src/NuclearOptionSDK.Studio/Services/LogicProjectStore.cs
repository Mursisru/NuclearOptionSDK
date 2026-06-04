using System.IO;
using Newtonsoft.Json;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public static class LogicProjectStore
{
    private static string LogicDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NuclearOptionSDK", "logic");

    public static string ProjectPath => Path.Combine(LogicDir, "project.json");
    public static string LayoutPath => Path.Combine(LogicDir, "layout.json");
    public static string NosdkDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NuclearOptionSDK", "projects");

    public static LogicProject Load()
    {
        if (!File.Exists(ProjectPath))
        {
            return CreateDefault();
        }

        var project = JsonConvert.DeserializeObject<LogicProject>(File.ReadAllText(ProjectPath)) ?? CreateDefault();
        if (project.layout != null)
        {
            project.layout = WorkspaceLayoutNormalizer.Normalize(project.layout);
        }

        return project;
    }

    public static void Save(LogicProject project)
    {
        Directory.CreateDirectory(LogicDir);
        File.WriteAllText(ProjectPath, JsonConvert.SerializeObject(project, Formatting.Indented));
    }

    public static LogicUILayout LoadLayout()
    {
        if (!File.Exists(LayoutPath))
        {
            return new LogicUILayout();
        }

        var raw = File.ReadAllText(LayoutPath);
        var layout = JsonConvert.DeserializeObject<LogicUILayout>(raw) ?? new LogicUILayout();
        var before = JsonConvert.SerializeObject(layout);
        layout = WorkspaceLayoutNormalizer.Normalize(layout);
        if (!string.Equals(before, JsonConvert.SerializeObject(layout), StringComparison.Ordinal))
        {
            SaveLayout(layout);
        }

        return layout;
    }

    public static void SaveLayout(LogicUILayout layout)
    {
        Directory.CreateDirectory(LogicDir);
        File.WriteAllText(LayoutPath, JsonConvert.SerializeObject(layout, Formatting.Indented));
    }

    public static ReferenceGraphPayload LoadReference(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Defaults", "reference-graphs", $"{id}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Reference graph not found: {id}", path);
        }

        return JsonConvert.DeserializeObject<ReferenceGraphPayload>(File.ReadAllText(path))
               ?? throw new InvalidDataException($"Invalid reference graph: {id}");
    }

    public static IReadOnlyList<ReferenceGraphPayload> LoadAllReferences()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Defaults", "reference-graphs");
        if (!Directory.Exists(dir))
        {
            return Array.Empty<ReferenceGraphPayload>();
        }

        return Directory.GetFiles(dir, "*.json")
            .Select(f => JsonConvert.DeserializeObject<ReferenceGraphPayload>(File.ReadAllText(f)))
            .Where(r => r != null)
            .Cast<ReferenceGraphPayload>()
            .ToList();
    }

    public static void SaveNosdk(NosdkProject project, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonConvert.SerializeObject(project, Formatting.Indented));
    }

    public static NosdkProject LoadNosdk(string filePath)
    {
        return JsonConvert.DeserializeObject<NosdkProject>(File.ReadAllText(filePath)) ?? new NosdkProject();
    }

    private static LogicProject CreateDefault()
    {
        var refGraph = LoadReferenceSafe("aoa-display");
        return new LogicProject
        {
            name = "logic-project",
            layout = new LogicUILayout { splitRatio = WorkspaceLayoutNormalizer.DefaultConstructorSplit },
            referenceId = "aoa-display",
            referenceGraph = refGraph?.graph ?? new LogicGraph(),
            userGraph = new LogicGraph
            {
                nodes = new[]
                {
                    new LogicNode { id = "src-aoa", kind = "source", typeId = "Telemetry.AoA", x = 40, y = 80 },
                    new LogicNode { id = "chk-15", kind = "check", typeId = "Compare.GreaterThan", x = 240, y = 80, parameters = new Dictionary<string, string> { ["threshold"] = "15" } },
                    new LogicNode { id = "out-red", kind = "output", typeId = "Action.SetOverlayColor", x = 440, y = 80, parameters = new Dictionary<string, string> { ["labelId"] = "aoa-label", ["colorHtml"] = "#FF0000" } }
                },
                edges = new[]
                {
                    new LogicEdge { fromNode = "src-aoa", toNode = "chk-15" },
                    new LogicEdge { fromNode = "chk-15", toNode = "out-red" }
                }
            }
        };
    }

    private static ReferenceGraphPayload? LoadReferenceSafe(string id)
    {
        try
        {
            return LoadReference(id);
        }
        catch
        {
            return null;
        }
    }
}
