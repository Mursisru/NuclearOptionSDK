using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>One output change type: checkbox + typed value.</summary>
public sealed record OutputChangeDef(
    string Id,
    string Label,
    LogicParamKind ValueKind,
    string DefaultValue,
    string? Placeholder = null,
    bool IsCatalog = false);

public static class LogicOutputChangeCatalog
{
    public static readonly OutputChangeDef[] BuiltIn =
    [
        new("color", "Overlay / HUD color", LogicParamKind.Color, "#FF4400"),
        new("text", "HUD text", LogicParamKind.Text, "AoA", "STALL / AoA / FUEL LOW"),
        new("format", "Number format", LogicParamKind.Text, "{0:F0}°", "{0:F0} / {0:F1} / {0} G"),
        new("visible", "Visibility (SetActive)", LogicParamKind.Bool, "true"),
        new("fontSize", "Font size", LogicParamKind.Number, "14"),
        new("alpha", "Alpha", LogicParamKind.Number, "1", "0–1"),
        new("positionX", "Position X", LogicParamKind.Number, "0"),
        new("positionY", "Position Y", LogicParamKind.Number, "0"),
        new("scale", "Scale", LogicParamKind.Number, "1"),
        new("rotation", "Rotation (°)", LogicParamKind.Number, "0"),
        new("materialColor", "Material.color", LogicParamKind.Color, "#FFFFFF"),
        new("spriteTint", "Sprite tint", LogicParamKind.Color, "#FFFFFF"),
        new("gValue", "G-load", LogicParamKind.Number, "3", "0–9"),
        new("aoaValue", "AoA (°)", LogicParamKind.Number, "15", "0–50"),
        new("speedValue", "Speed", LogicParamKind.Number, "100", "m/s"),
        new("altValue", "Altitude", LogicParamKind.Number, "500", "m AGL"),
        new("fuelValue", "Fuel", LogicParamKind.Number, "0.15", "0–1"),
        new("sound", "Sound", LogicParamKind.Text, "stallHorn", ".wav name"),
        new("volume", "Sound volume", LogicParamKind.Number, "1", "0–1"),
        new("fadeSec", "Fade (sec)", LogicParamKind.Number, "0.5"),
        new("animatorTrigger", "Animator Trigger", LogicParamKind.Text, "Play"),
        new("animatorBool", "Animator Bool", LogicParamKind.Bool, "true"),
        new("animatorFloat", "Animator Float", LogicParamKind.Number, "1"),
        new("stateKey", "State key", LogicParamKind.Text, "state.flag"),
        new("stateVal", "State value", LogicParamKind.Text, "true")
    ];

    private static readonly Lazy<OutputChangeDef[]> CatalogLazy = new(BuildCatalogChanges);

    public static OutputChangeDef[] All => BuiltIn.Concat(CatalogLazy.Value).ToArray();

    public static OutputChangeDef[] CatalogChanges => CatalogLazy.Value;

    public static string OnKey(string id) => $"out.{id}.on";
    public static string ValKey(string id) => $"out.{id}.val";

    public static bool IsEnabled(LogicNode node, string id) =>
        node.parameters.TryGetValue(OnKey(id), out var v) && v == "true";

    public static string GetValue(LogicNode node, string id, string fallback = "") =>
        node.parameters.TryGetValue(ValKey(id), out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

    public static IEnumerable<OutputChangeDef> EnabledChanges(LogicNode node)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in BuiltIn)
        {
            if (IsEnabled(node, def.Id))
            {
                seen.Add(def.Id);
                yield return def;
            }
        }

        foreach (var key in node.parameters.Keys)
        {
            if (!key.StartsWith("out.", StringComparison.Ordinal) || !key.EndsWith(".on", StringComparison.Ordinal))
            {
                continue;
            }

            var id = key["out.".Length..^".on".Length];
            if (!seen.Add(id))
            {
                continue;
            }

            if (IsEnabled(node, id))
            {
                yield return ResolveDef(id);
            }
        }
    }

    public static OutputChangeDef ResolveDef(string id)
    {
        var builtIn = BuiltIn.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        if (builtIn != null)
        {
            return builtIn;
        }

        if (id.StartsWith("Member.", StringComparison.Ordinal))
        {
            var clr = GameCodeIndexCache.TryGetClrType(id);
            var kind = !string.IsNullOrWhiteSpace(clr)
                ? GameBindingValueSchema.ClassifyClrType(clr)
                : LogicParamKind.Bool;
            var defaultValue = kind switch
            {
                LogicParamKind.Bool => "false",
                LogicParamKind.Number => "0",
                _ => string.Empty
            };
            return new OutputChangeDef(
                id,
                LogicParamCatalog.WatchParamFriendlyTitle(id, null),
                kind,
                defaultValue,
                id,
                IsCatalog: true);
        }

        if (NoGameParameterCatalog.TryGet(id, out var entry))
        {
            return new OutputChangeDef(
                id,
                NoGameParameterCatalog.Title(id),
                MapValueKind(entry.ValueType),
                DefaultForType(entry.ValueType),
                entry.GamePath,
                IsCatalog: true);
        }

        return new OutputChangeDef(id, id, LogicParamKind.Text, string.Empty, IsCatalog: true);
    }

    public static string InferWriteTarget(string sourceOrReadId)
    {
        if (string.IsNullOrWhiteSpace(sourceOrReadId))
        {
            return string.Empty;
        }

        if (sourceOrReadId.StartsWith("Write.", StringComparison.Ordinal)
            || sourceOrReadId.StartsWith("UI.", StringComparison.Ordinal))
        {
            return sourceOrReadId;
        }

        if (sourceOrReadId.StartsWith("Read.", StringComparison.Ordinal))
        {
            return "Write." + sourceOrReadId["Read.".Length..];
        }

        return sourceOrReadId;
    }

    public static void ImportLegacySingleAction(LogicNode node)
    {
        if (EnabledChanges(node).Any())
        {
            return;
        }

        switch (node.typeId)
        {
            case "Action.SetOverlayColor":
            case "Action.SetHudColor":
                Set(node, "color", true, GetParam(node, "colorHtml", "#FF4400"));
                break;
            case "Action.SetOverlayVisible":
            case "Action.SetHudActive":
                Set(node, "visible", true, GetParam(node, "visible", "true"));
                break;
            case "Action.SetHudText":
            case "Action.CreateOverlayLabel":
                Set(node, "text", true, GetParam(node, "text", "AoA"));
                if (node.parameters.TryGetValue("formatString", out var fmt) && !string.IsNullOrWhiteSpace(fmt))
                {
                    Set(node, "format", true, fmt);
                }

                break;
            case "Action.SetFontSize":
                Set(node, "fontSize", true, GetParam(node, "fontSize", "14"));
                break;
            case "Action.PlaySound":
            case "Audio.Action.PlayClip":
                Set(node, "sound", true, GetParam(node, "clipName", "stallHorn"));
                if (node.parameters.TryGetValue("volume", out var vol) && !string.IsNullOrWhiteSpace(vol))
                {
                    Set(node, "volume", true, vol);
                }

                break;
            case "Action.FadeIn":
            case "Action.FadeOut":
                Set(node, "fadeSec", true, GetParam(node, "durationSec", "0.5"));
                break;
            case "Logic.SetState":
                Set(node, "stateKey", true, GetParam(node, "key", "state.flag"));
                Set(node, "stateVal", true, GetParam(node, "value", "true"));
                break;
        }
    }

    private static OutputChangeDef[] BuildCatalogChanges() =>
        NoGameParameterCatalog.FlatTargets()
            .Where(id => !BuiltIn.Any(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Select(id =>
            {
                NoGameParameterCatalog.TryGet(id, out var entry);
                return new OutputChangeDef(
                    id,
                    NoGameParameterCatalog.Title(id),
                    MapValueKind(entry?.ValueType ?? "string"),
                    DefaultForType(entry?.ValueType ?? "string"),
                    entry?.GamePath,
                    IsCatalog: true);
            })
            .ToArray();

    private static LogicParamKind MapValueKind(string valueType) => valueType switch
    {
        "float" or "double" or "int" or "long" or "short" or "byte" => LogicParamKind.Number,
        "bool" => LogicParamKind.Bool,
        "Color" => LogicParamKind.Color,
        _ => LogicParamKind.Text
    };

    private static string DefaultForType(string valueType) => valueType switch
    {
        "float" or "double" => "0",
        "int" or "long" or "short" or "byte" => "0",
        "bool" => "true",
        "Color" => "#FFFFFF",
        _ => string.Empty
    };

    private static string GetParam(LogicNode node, string key, string fallback = "") =>
        node.parameters.TryGetValue(key, out var v) ? v : fallback;

    private static void Set(LogicNode node, string id, bool on, string val)
    {
        node.parameters[OnKey(id)] = on ? "true" : "false";
        node.parameters[ValKey(id)] = val;
    }
}
