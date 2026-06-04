using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services.ApiSurface.Context;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Тип и формат expectValue для check-узлов по clrType / bindingId.</summary>
public static class GameBindingValueSchema
{
    public const string ClrTypeParameterKey = "clrType";
    public const string ValueKindParameterKey = "valueKind";

    public static LogicParamKind ResolveExpectValueKind(LogicNode checkNode, LogicGraph? graph = null)
    {
        if (checkNode.typeId is "Compare.Changed" or "Compare.IsTrue" or "Compare.IsFalse")
        {
            return LogicParamKind.Bool;
        }

        return ClassifyClrType(ResolveClrType(checkNode, graph));
    }

    public static string? ResolveClrType(LogicNode node, LogicGraph? graph = null)
    {
        if (node.parameters.TryGetValue(ClrTypeParameterKey, out var clr) && !string.IsNullOrWhiteSpace(clr))
        {
            return clr.Trim();
        }

        if (node.parameters.TryGetValue(ValueKindParameterKey, out var vk) && !string.IsNullOrWhiteSpace(vk))
        {
            return vk.Trim();
        }

        if (node.parameters.TryGetValue("watchParam", out var watch) && !string.IsNullOrWhiteSpace(watch))
        {
            var fromSource = TryResolveClrFromSourceBinding(graph, watch);
            if (!string.IsNullOrWhiteSpace(fromSource))
            {
                return fromSource;
            }
        }

        return InferClrFromWatchParam(watch);
    }

    public static LogicParamKind ClassifyClrType(string? clrTypeName)
    {
        if (string.IsNullOrWhiteSpace(clrTypeName))
        {
            return LogicParamKind.Number;
        }

        var bare = StripNullable(clrTypeName.Trim());
        if (IsBoolClr(bare))
        {
            return LogicParamKind.Bool;
        }

        if (IsStringClr(bare))
        {
            return LogicParamKind.Text;
        }

        return ClrTypeClassifier.Classify(bare) switch
        {
            ClrTypeKind.String => LogicParamKind.Text,
            ClrTypeKind.Primitive => LogicParamKind.Number,
            _ => LogicParamKind.Number
        };
    }

    public static bool IsBoolClrType(string? clrTypeName) =>
        !string.IsNullOrWhiteSpace(clrTypeName) && IsBoolClr(StripNullable(clrTypeName.Trim()));

    public static void ApplyWatchParamMetadata(LogicNode checkNode, LogicGraph? graph = null)
    {
        if (checkNode.kind is not ("check" or "gate"))
        {
            return;
        }

        if (!checkNode.parameters.TryGetValue("watchParam", out var watch) || string.IsNullOrWhiteSpace(watch))
        {
            return;
        }

        var clr = TryResolveClrFromSourceBinding(graph, watch)
                  ?? GameCodeIndexCache.TryGetClrType(watch)
                  ?? InferClrFromWatchParam(watch);
        if (!string.IsNullOrWhiteSpace(clr))
        {
            checkNode.parameters[ClrTypeParameterKey] = clr.Trim();
        }
    }

    public static void ApplyDefaultExpectForKind(LogicNode checkNode, LogicGraph? graph = null)
    {
        if (checkNode.kind is not ("check" or "gate"))
        {
            return;
        }

        if (checkNode.typeId is "Compare.InRange" or "Compare.OutsideRange"
            or "Compare.Hysteresis" or "Compare.Changed"
            or "Compare.IsTrue" or "Compare.IsFalse")
        {
            return;
        }

        var kind = ResolveExpectValueKind(checkNode, graph);
        var value = DefaultExpectValue(checkNode, kind);
        checkNode.parameters["expectValue"] = value;
        checkNode.parameters["threshold"] = value;
    }

    public static string DefaultExpectValue(LogicNode checkNode, LogicParamKind kind) => kind switch
    {
        LogicParamKind.Bool => "true",
        LogicParamKind.Text => string.Empty,
        LogicParamKind.Number => checkNode.typeId switch
        {
            "Compare.LessThan" or "Compare.LessOrEqual" => "100",
            "Compare.Equals" or "Compare.NotEquals" or "Compare.Approximately" => "0",
            _ => "15"
        },
        _ => "15"
    };

    public static bool TryNormalizeExpectValue(string? raw, LogicParamKind kind, out string normalized)
    {
        normalized = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
        {
            normalized = DefaultExpectValue(new LogicNode { typeId = "Compare.Equals", kind = "check" }, kind);
            return true;
        }

        switch (kind)
        {
            case LogicParamKind.Bool:
                if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || normalized is "1" or "yes" or "да")
                {
                    normalized = "true";
                    return true;
                }

                if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
                    || normalized is "0" or "no" or "нет")
                {
                    normalized = "false";
                    return true;
                }

                return false;
            case LogicParamKind.Number:
                return double.TryParse(
                    normalized,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _);
            default:
                return true;
        }
    }

    public static string FormatExpectDisplay(string? value, LogicParamKind kind) => kind switch
    {
        LogicParamKind.Bool => value == "false" ? "no" : "yes",
        _ => value ?? string.Empty
    };

    public static void CopyBindingMetadata(IDictionary<string, string> target, GameMemberNode member)
    {
        target["bindingId"] = member.BindingId;
        if (!string.IsNullOrWhiteSpace(member.ClrTypeName))
        {
            target[ClrTypeParameterKey] = member.ClrTypeName;
        }
    }

    public static void ApplyClrTypeFromDrag(IDictionary<string, string> parameters, string? clrType)
    {
        if (!string.IsNullOrWhiteSpace(clrType))
        {
            parameters[ClrTypeParameterKey] = clrType.Trim();
        }
    }

    private static string? TryResolveClrFromSourceBinding(LogicGraph? graph, string watchParam)
    {
        if (graph == null)
        {
            return null;
        }

        foreach (var source in graph.nodes.Where(n => n.kind == "source"))
        {
            if (source.typeId == "Member.Bind"
                && source.parameters.TryGetValue("bindingId", out var bid)
                && string.Equals(bid, watchParam, StringComparison.OrdinalIgnoreCase)
                && source.parameters.TryGetValue(ClrTypeParameterKey, out var clr)
                && !string.IsNullOrWhiteSpace(clr))
            {
                return clr;
            }

            if (string.Equals(source.typeId, watchParam, StringComparison.OrdinalIgnoreCase)
                && source.parameters.TryGetValue(ClrTypeParameterKey, out var typeClr)
                && !string.IsNullOrWhiteSpace(typeClr))
            {
                return typeClr;
            }
        }

        return null;
    }

    private static string? InferClrFromWatchParam(string? watchParam)
    {
        if (string.IsNullOrWhiteSpace(watchParam))
        {
            return null;
        }

        if (NoGameParameterCatalog.TryGet(watchParam, out var entry) && !string.IsNullOrWhiteSpace(entry.ValueType))
        {
            return entry.ValueType;
        }

        return GameCodeIndexCache.TryGetClrType(watchParam);
    }

    private static bool IsBoolClr(string bare) =>
        bare.Equals("bool", StringComparison.OrdinalIgnoreCase)
        || bare.Equals("Boolean", StringComparison.OrdinalIgnoreCase);

    private static bool IsStringClr(string bare) =>
        bare.Equals("string", StringComparison.OrdinalIgnoreCase)
        || bare.Equals("String", StringComparison.OrdinalIgnoreCase);

    private static string StripNullable(string clr) =>
        clr.EndsWith('?') ? clr[..^1] : clr;
}
