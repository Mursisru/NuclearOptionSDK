using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public sealed record PresetGroup(string Title, IReadOnlyList<string> Values);

/// <summary>Контекстные подсказки для ПКМ-меню (Inspector использует LogicParamCatalog напрямую).</summary>
public static class LogicParamPresetBank
{
    public static IReadOnlyList<PresetGroup> GetGroups(LogicParamField field, LogicNode node)
    {
        var flat = FlatList(field, node);
        return flat.Count == 0 ? [] : [new PresetGroup(string.Empty, flat)];
    }

    public static IReadOnlyList<string> FlatList(LogicParamField field, LogicNode node) =>
        field.Kind switch
        {
            LogicParamKind.Choice => field.Choices?.ToList() ?? [],
            LogicParamKind.Number when field.Key is "expectValue" or "threshold" or "min" or "max" or "onThreshold" or "offThreshold"
                => LogicParamCatalog.QuickValuesForParam(LogicParamCatalog.ResolveContextParam(node)).ToList(),
            LogicParamKind.Number when field.Key is "holdSeconds" or "seconds" or "delaySec" or "durationSec"
                => LogicParamCatalog.QuickValuesForParam("time").ToList(),
            _ => []
        };
}
