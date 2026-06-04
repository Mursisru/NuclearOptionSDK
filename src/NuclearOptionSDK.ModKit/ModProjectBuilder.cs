using System.Diagnostics;
using System.Text;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.ModKit;

public static class ModProjectBuilder
{
    public static ModBuildResponse Build(ModBuildRequest request, string nuclearOptionRoot)
    {
        if (string.IsNullOrWhiteSpace(request.modName))
        {
            return Fail("Mod name is required.");
        }

        var outputRoot = string.IsNullOrWhiteSpace(request.outputDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NuclearOptionSDK", "Mods", request.modName)
            : request.outputDirectory;

        try
        {
            Directory.CreateDirectory(outputRoot);
            var projectDir = Path.Combine(outputRoot, $"{request.modName}_Engine");
            Directory.CreateDirectory(projectDir);

            var assemblyName = $"{request.modName}_Engine";
            var pluginFile = Path.Combine(projectDir, $"{request.modName}Plugin.cs");
            var csprojFile = Path.Combine(projectDir, $"{assemblyName}.csproj");

            File.WriteAllText(pluginFile, string.IsNullOrWhiteSpace(request.pluginSourceOverride)
                ? BuildPluginSource(request, assemblyName)
                : request.pluginSourceOverride);
            foreach (var extra in request.extraSourceFiles)
            {
                if (!File.Exists(extra))
                {
                    continue;
                }

                var dest = Path.Combine(projectDir, Path.GetFileName(extra));
                File.Copy(extra, dest, true);
            }

            try
            {
                ModRuntimeFileEmitter.EmitGameBindingRuntime(projectDir, assemblyName);
            }
            catch (Exception ex)
            {
                return Fail($"GameBindingRuntime template: {ex.Message}");
            }

            var compileFiles = Directory.GetFiles(projectDir, "*.cs")
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (compileFiles.Count == 0)
            {
                return Fail("No .cs source files in mod project.");
            }

            var gameReferences = GameAssemblyReferenceCollector.Collect(nuclearOptionRoot);
            if (gameReferences.Count == 0 && string.IsNullOrWhiteSpace(request.csprojOverride))
            {
                return Fail(
                    "Game assemblies not found. Set the Nuclear Option install path in Studio Settings " +
                    "(need NuclearOption_Data\\Managed and BepInEx\\core).");
            }

            var csprojContent = string.IsNullOrWhiteSpace(request.csprojOverride)
                ? ModCsprojTemplate.Build(assemblyName, nuclearOptionRoot, compileFiles, gameReferences)
                : request.csprojOverride!.Replace("$(NuclearOptionRoot)", nuclearOptionRoot);
            File.WriteAllText(csprojFile, csprojContent);

            var log = new StringBuilder();
            if (gameReferences.Count > 0)
            {
                log.AppendLine($"Game assembly references: {gameReferences.Count}");
            }
            var exitCode = RunMsBuild(csprojFile, log);
            var dllPath = Path.Combine(projectDir, "bin", "Release", $"{assemblyName}.dll");

            if (exitCode != 0 || !File.Exists(dllPath))
            {
                return new ModBuildResponse
                {
                    success = false,
                    error = $"MSBuild failed with exit code {exitCode}.",
                    buildLog = log.ToString(),
                    outputPath = projectDir
                };
            }

            var pluginsDir = Path.Combine(nuclearOptionRoot, "BepInEx", "plugins");
            Directory.CreateDirectory(pluginsDir);
            File.Copy(dllPath, Path.Combine(pluginsDir, $"{assemblyName}.dll"), true);

            return new ModBuildResponse
            {
                success = true,
                outputPath = dllPath,
                buildLog = log.ToString()
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.ToString());
        }
    }

    private static ModBuildResponse Fail(string error)
    {
        return new ModBuildResponse { success = false, error = error };
    }

    private static string BuildPluginSource(ModBuildRequest request, string assemblyName)
    {
        return $@"using BepInEx;
using HarmonyLib;

namespace {request.modName}_Engine;

[BepInPlugin(""{request.pluginGuid}"", ""{request.modName}"", ""1.0.0"")]
public sealed class {request.modName}Plugin : BaseUnityPlugin
{{
    private void Awake()
    {{
        new Harmony(""{request.pluginGuid}"").PatchAll();
        Logger.LogInfo(""{request.modName} loaded."");
    }}
}}
";
    }

    private static int RunMsBuild(string csprojFile, StringBuilder log)
    {
        var msbuild = MsBuildLocator.TryResolve(log);
        if (msbuild == null)
        {
            log.AppendLine("MSBuild not found. Install Visual Studio or Build Tools with MSBuild workload.");
            return -1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = msbuild,
            Arguments = $"\"{csprojFile}\" /p:Configuration=Release /p:Platform=AnyCPU /nologo /v:m",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            log.AppendLine("Failed to start MSBuild.");
            return -1;
        }

        log.AppendLine(process.StandardOutput.ReadToEnd());
        log.AppendLine(process.StandardError.ReadToEnd());
        process.WaitForExit();
        return process.ExitCode;
    }

}
