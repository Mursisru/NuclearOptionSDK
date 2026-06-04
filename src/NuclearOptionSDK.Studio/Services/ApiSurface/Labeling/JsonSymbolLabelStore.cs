using System.Reflection;
using System.Text.Json;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;

public sealed class JsonSymbolLabelStore
{
    private readonly Dictionary<string, SymbolLabel> _types;
    private readonly Dictionary<string, SymbolLabel> _members;

    private JsonSymbolLabelStore(Dictionary<string, SymbolLabel> types, Dictionary<string, SymbolLabel> members)
    {
        _types = types;
        _members = members;
    }

    public static JsonSymbolLabelStore LoadDefault()
    {
        var types = new Dictionary<string, SymbolLabel>(StringComparer.OrdinalIgnoreCase);
        var members = new Dictionary<string, SymbolLabel>(StringComparer.OrdinalIgnoreCase);

        var json = ReadEmbedded("locales.en.api-symbols.json")
                   ?? ReadFile(Path.Combine(AppContext.BaseDirectory, "Defaults", "locales", "en", "api-symbols.json"));
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonSymbolLabelStore(types, members);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("types", out var typesEl))
            {
                foreach (var prop in typesEl.EnumerateObject())
                {
                    types[prop.Name] = ParseLabel(prop.Value);
                }
            }

            if (doc.RootElement.TryGetProperty("members", out var membersEl))
            {
                foreach (var prop in membersEl.EnumerateObject())
                {
                    members[prop.Name] = ParseLabel(prop.Value);
                }
            }
        }
        catch
        {
            // ignore malformed
        }

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NuclearOptionSDK",
            "locales",
            "en",
            "api-symbols.json");
        if (File.Exists(appData))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(appData));
                Merge(doc, types, members);
            }
            catch
            {
                // ignore
            }
        }

        return new JsonSymbolLabelStore(types, members);
    }

    public bool TryGetType(string shortOrFullName, out SymbolLabel label) =>
        _types.TryGetValue(shortOrFullName, out label!)
        || _types.TryGetValue(ShortName(shortOrFullName), out label!);

    public bool TryGetMember(string key, out SymbolLabel label) =>
        _members.TryGetValue(key, out label!);

    private static void Merge(JsonDocument doc, Dictionary<string, SymbolLabel> types, Dictionary<string, SymbolLabel> members)
    {
        if (doc.RootElement.TryGetProperty("types", out var typesEl))
        {
            foreach (var prop in typesEl.EnumerateObject())
            {
                types[prop.Name] = ParseLabel(prop.Value);
            }
        }

        if (doc.RootElement.TryGetProperty("members", out var membersEl))
        {
            foreach (var prop in membersEl.EnumerateObject())
            {
                members[prop.Name] = ParseLabel(prop.Value);
            }
        }
    }

    private static SymbolLabel ParseLabel(JsonElement el)
    {
        var title = el.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        var hint = el.TryGetProperty("hint", out var h) ? h.GetString() : null;
        var tag = el.TryGetProperty("tag", out var g) ? g.GetString() : null;
        return new SymbolLabel(title, hint, tag);
    }

    private static string? ReadEmbedded(string resourceSuffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (name == null)
        {
            return null;
        }

        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? ReadFile(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static string ShortName(string typeFullName) =>
        typeFullName.Contains('.')
            ? typeFullName[(typeFullName.LastIndexOf('.') + 1)..]
            : typeFullName;
}
