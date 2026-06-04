using System.Linq;

namespace NuclearOptionSDK.ModKit;

/// <summary>All managed DLLs from the game install (Managed + BepInEx core) for mod .csproj references.</summary>
public static class GameAssemblyReferenceCollector
{
    public sealed class GameAssemblyReference
    {
        public GameAssemblyReference(string assemblyName, string hintPath)
        {
            AssemblyName = assemblyName;
            HintPath = hintPath;
        }

        public string AssemblyName { get; }
        public string HintPath { get; }
    }

    public static IReadOnlyList<GameAssemblyReference> Collect(string nuclearOptionRoot)
    {
        if (string.IsNullOrWhiteSpace(nuclearOptionRoot) || !Directory.Exists(nuclearOptionRoot))
        {
            return Array.Empty<GameAssemblyReference>();
        }

        var root = nuclearOptionRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void AddDirectory(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            foreach (var path in files)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!byName.ContainsKey(name))
                {
                    byName[name] = path;
                }
            }
        }

        // Game assemblies first; BepInEx core fills gaps (Harmony, etc.).
        AddDirectory(Path.Combine(root, "NuclearOption_Data", "Managed"));
        AddDirectory(Path.Combine(root, "BepInEx", "core"));

        // Prefer modern Harmony (0Harmony) to avoid CS0433 conflicts with legacy 0Harmony20.
        if (byName.ContainsKey("0Harmony") && byName.ContainsKey("0Harmony20"))
        {
            byName.Remove("0Harmony20");
        }

        return byName
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new GameAssemblyReference(kv.Key, kv.Value))
            .ToList();
    }
}
