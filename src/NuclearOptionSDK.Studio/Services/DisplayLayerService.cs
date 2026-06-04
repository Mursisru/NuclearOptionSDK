using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public sealed class DisplayLayerService
{
    private readonly Dictionary<string, DisplayEntry> _logic = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DisplayEntry> _bindings = new(StringComparer.Ordinal);

    public DisplayLayerService()
    {
        LoadEmbedded("NuclearOptionSDK.Studio.locales.en.logic.json", _logic);
        LoadEmbedded("NuclearOptionSDK.Studio.locales.en.bindings.json", _bindings, "bindings");
    }

    public DisplayEntry Resolve(string typeId)
    {
        if (_logic.TryGetValue(typeId, out var entry))
        {
            return entry;
        }

        if (_bindings.TryGetValue(typeId, out entry))
        {
            return entry;
        }

        if (NoGameParameterCatalog.TryGet(typeId, out var param) ||
            NoGameParameterCatalog.TryGetMethod(typeId, out _))
        {
            return new DisplayEntry
            {
                id = typeId,
                title = NoGameParameterCatalog.Title(typeId),
                hint = NoGameParameterCatalog.Hint(typeId),
                category = GuessCategory(typeId)
            };
        }

        if (LogicCheckCatalog.IsKnownCheck(typeId) ||
            typeId.StartsWith("Compare.", StringComparison.Ordinal) ||
            typeId.StartsWith("Gate.", StringComparison.Ordinal))
        {
            return new DisplayEntry
            {
                id = typeId,
                title = LogicCheckCatalog.Title(typeId),
                hint = LogicCheckCatalog.Hint(typeId),
                category = "check"
            };
        }

        return new DisplayEntry
        {
            id = typeId,
            title = AutoLabel(typeId),
            hint = typeId,
            category = GuessCategory(typeId)
        };
    }

    public string Title(string typeId) => Resolve(typeId).title;
    public string Hint(string typeId) => Resolve(typeId).hint;

    public IReadOnlyList<DisplayEntry> PaletteEntries(string category)
        => _logic.Values.Where(e => e.category == category).OrderBy(e => e.title).ToList();

    public IReadOnlyList<DisplayEntry> AllLogicEntries() => _logic.Values.OrderBy(e => e.title).ToList();

    private static void LoadEmbedded(string resourceName, Dictionary<string, DisplayEntry> target, string arrayKey = "entries")
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return;
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var root = JsonConvert.DeserializeObject<Dictionary<string, DisplayEntry[]>>(json);
        if (root == null || !root.TryGetValue(arrayKey, out var entries))
        {
            return;
        }

        foreach (var entry in entries)
        {
            target[entry.id] = entry;
        }
    }

    private static string AutoLabel(string typeId)
    {
        var tail = typeId.Contains('.') ? typeId[(typeId.LastIndexOf('.') + 1)..] : typeId;
        return Regex.Replace(tail, "([a-z])([A-Z])", "$1 $2", RegexOptions.None, TimeSpan.FromMilliseconds(50));
    }

    private static string GuessCategory(string typeId)
    {
        if (typeId.StartsWith("Telemetry.", StringComparison.Ordinal)) return "telemetry";
        if (typeId.StartsWith("Compare.", StringComparison.Ordinal)) return "check";
        if (typeId.StartsWith("Gate.", StringComparison.Ordinal)) return "gate";
        if (typeId.StartsWith("Action.", StringComparison.Ordinal)) return "action";
        return "other";
    }
}
