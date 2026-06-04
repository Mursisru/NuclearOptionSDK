using Newtonsoft.Json;
using NuclearOptionSDK.ModKit;
using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;

const string defaultGameRoot =
    @"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option";

var gameRoot = args.Length > 0 && Directory.Exists(args[0])
    ? args[0]
    : defaultGameRoot;

var projectPath = args.Length > 1
    ? args[1]
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NuclearOptionSDK",
        "logic",
        "project.json");
var diagnosticMode = args.Any(a => string.Equals(a, "--diagnostic", StringComparison.OrdinalIgnoreCase));
var patchMode = args.Any(a => string.Equals(a, "--patch", StringComparison.OrdinalIgnoreCase));

if (!Directory.Exists(gameRoot))
{
    Console.Error.WriteLine($"Game folder not found: {gameRoot}");
    return 1;
}

if (!File.Exists(projectPath))
{
    Console.Error.WriteLine($"Project not found: {projectPath}");
    return 1;
}

GameCodeIndexBootstrap.EnsureLoaded(gameRoot);

var project = JsonConvert.DeserializeObject<LogicProject>(File.ReadAllText(projectPath)) ?? new LogicProject();
project.diagnosticMode = diagnosticMode;
if (patchMode)
{
    project.executionMode = "Patch";
    project.patchTargetType ??= "Aircraft";
    project.patchMethodName ??= "Update";
    project.patchKind = "Postfix";
}
var mods = new (string Name, string Guid)[]
{
    ("StudioTestGear", "com.nuclearstudio.studiotestgear"),
    ("LogicMod", "com.nuclearstudio.logicmod")
};

var failed = false;
foreach (var (modName, pluginGuid) in mods)
{
    var source = LogicModSourceGenerator.Generate(project, modName, pluginGuid);
    var outputDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NuclearOptionSDK",
        "projects",
        modName);

    var request = LogicModExporter.CreateBuildRequest(modName, pluginGuid, project, outputDir, source);
    var response = ModProjectBuilder.Build(request, gameRoot);
    var pluginsDll = Path.Combine(gameRoot, "BepInEx", "plugins", $"{modName}_Engine.dll");

    Console.WriteLine($"=== {modName} ===");
    Console.WriteLine(response.buildLog);
    if (!response.success)
    {
        Console.Error.WriteLine($"BUILD FAILED ({modName}): {response.error}");
        failed = true;
        continue;
    }

    Console.WriteLine($"Built: {response.outputPath}");
    Console.WriteLine($"Deployed: {pluginsDll} · {new FileInfo(pluginsDll).Length} bytes");
    Console.WriteLine();
}

return failed ? 1 : 0;
