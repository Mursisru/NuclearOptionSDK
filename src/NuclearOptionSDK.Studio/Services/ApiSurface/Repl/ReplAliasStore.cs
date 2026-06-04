using System.Text.Json;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Repl;

public sealed class ReplAliasStore
{
    private readonly Dictionary<string, string> _aliases;

    private ReplAliasStore(Dictionary<string, string> aliases) => _aliases = aliases;

    public static ReplAliasStore LoadDefault()
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Defaults", "repl-aliases.json");
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("aliases", out var el))
                {
                    foreach (var prop in el.EnumerateObject())
                    {
                        aliases[prop.Name] = prop.Value.GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        return new ReplAliasStore(aliases);
    }

    public IReadOnlyDictionary<string, string> Aliases => _aliases;
}
