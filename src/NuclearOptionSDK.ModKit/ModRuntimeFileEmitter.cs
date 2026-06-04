namespace NuclearOptionSDK.ModKit;

/// <summary>Copies shared runtime sources into a mod project before MSBuild.</summary>
public static class ModRuntimeFileEmitter
{
    private const string GameBindingsNamespace = "NuclearOptionSDK.GameBindings";

    public static void EmitGameBindingRuntime(string projectDir, string modNamespace)
    {
        var sourcePath = Path.Combine(
            Path.GetDirectoryName(typeof(ModRuntimeFileEmitter).Assembly.Location) ?? AppContext.BaseDirectory,
            "Runtime",
            "GameBindingRuntime.cs");

        if (!File.Exists(sourcePath))
        {
            sourcePath = LocateSourceFile("GameBindingRuntime.cs");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("GameBindingRuntime.cs template not found.", sourcePath);
        }

        var text = File.ReadAllText(sourcePath)
            .Replace(GameBindingsNamespace, modNamespace);
        File.WriteAllText(Path.Combine(projectDir, "GameBindingRuntime.cs"), text);
    }

    private static string LocateSourceFile(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "src", "NuclearOptionSDK.ModKit", "Runtime", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(dir, "NuclearOptionSDK.ModKit", "Runtime", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent == null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return Path.Combine(dir, fileName);
    }
}
