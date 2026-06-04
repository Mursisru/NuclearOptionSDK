namespace NuclearOptionSDK.Decompiler;

public static class GameAssemblyPaths
{
    public const string MainAssemblyFileName = "Assembly-CSharp.dll";

    public static string? ResolveMainAssembly(string? nuclearOptionRoot)
    {
        if (string.IsNullOrWhiteSpace(nuclearOptionRoot))
        {
            return null;
        }

        var path = Path.Combine(
            nuclearOptionRoot.Trim(),
            "NuclearOption_Data",
            "Managed",
            MainAssemblyFileName);

        return File.Exists(path) ? path : null;
    }

    public static string? ResolveManagedDirectory(string? nuclearOptionRoot)
    {
        if (string.IsNullOrWhiteSpace(nuclearOptionRoot))
        {
            return null;
        }

        var dir = Path.Combine(nuclearOptionRoot.Trim(), "NuclearOption_Data", "Managed");
        return Directory.Exists(dir) ? dir : null;
    }

    public static IReadOnlyList<string> EnumerateReferenceAssemblies(string managedDir)
    {
        if (!Directory.Exists(managedDir))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(managedDir, "*.dll", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
