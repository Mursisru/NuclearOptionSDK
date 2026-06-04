using System.Text.RegularExpressions;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;

public sealed class HumanizeLabelResolver : ILabelResolver
{
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AoA"] = "Angle of attack (AoA)",
        ["Hp"] = "HP",
        ["Rwr"] = "RWR",
        ["Mfd"] = "MFD",
        ["Ecm"] = "ECM",
        ["Rcs"] = "RCS",
        ["Gps"] = "GPS"
    };

    public bool TryResolveType(string typeFullName, out SymbolLabel label)
    {
        var shortName = typeFullName.Contains('.')
            ? typeFullName[(typeFullName.LastIndexOf('.') + 1)..]
            : typeFullName;
        label = new SymbolLabel(Humanize(shortName));
        return true;
    }

    public bool TryResolveMember(string typeFullName, string memberName, out SymbolLabel label)
    {
        label = new SymbolLabel(Humanize(memberName));
        return true;
    }

    public static string Humanize(string name)
    {
        if (Abbreviations.TryGetValue(name, out var full))
        {
            return full;
        }

        var spaced = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        spaced = Regex.Replace(spaced, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        return spaced;
    }
}
