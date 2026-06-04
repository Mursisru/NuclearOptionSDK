using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public enum LogicParamKind
{
    Text,
    Number,
    Color,
    Bool,
    Choice,
    Binding
}

public enum LogicParamSection
{
    Source,
    Check,
    Output
}

public sealed record LogicParamField(
    string Key,
    LogicParamKind Kind,
    string Label,
    LogicParamSection Section,
    string? Placeholder = null,
    IReadOnlyList<string>? Choices = null);

public static class LogicNodeParameterSchema
{
    public static IReadOnlyList<LogicParamField> GetFields(LogicNode node, LogicGraph? graph = null)
    {
        var fields = new List<LogicParamField>();

        switch (node.kind)
        {
            case "source":
                AppendSourceFields(node, fields);
                break;
            case "check":
            case "gate":
                AppendCheckFields(node, fields, graph);
                break;
            case "output":
                AppendOutputFields(node, fields);
                break;
            default:
                AppendFieldsForType(node.typeId, fields);
                break;
        }

        if (node.parameters.ContainsKey("displayName"))
        {
            fields.Insert(0, Field("displayName", LogicParamKind.Text, "Block label", LogicParamSection.Source));
        }

        return fields;
    }

    private static void AppendSourceFields(LogicNode node, List<LogicParamField> fields)
    {
        // typeId блока = параметр из дампа; отдельного sourceParam в инспекторе нет.

        if (node.typeId == "Member.Bind" || node.parameters.ContainsKey("bindingId"))
        {
            fields.Add(Bind("bindingId", "API binding"));
        }

        if (node.typeId == "Telemetry.Constant" || node.parameters.ContainsKey("value"))
        {
            fields.Add(Num("value", "Constant value", "0", LogicParamSection.Source));
        }
    }

    private static void AppendCheckFields(LogicNode node, List<LogicParamField> fields, LogicGraph? graph)
    {
        fields.Add(WatchParamField());

        AppendExpectValueFields(node, fields, graph);

        if (NeedsHoldSeconds(node.typeId))
        {
            fields.Add(Sec("holdSeconds", HoldSecondsLabel(node.typeId), HoldSecondsDefault(node.typeId)));
        }

        if (node.typeId is "Compare.Hysteresis")
        {
            fields.Add(Num("offThreshold", "Off threshold", "10", LogicParamSection.Check));
        }

        if (node.typeId is "Compare.Approximately")
        {
            fields.Add(Num("epsilon", "Tolerance (ε)", "0.01", LogicParamSection.Check));
        }
    }

    private static LogicParamField WatchParamField() =>
        Choice(
            "watchParam",
            "Which parameter to check",
            Array.Empty<string>(),
            string.Empty,
            LogicParamSection.Check);

    private static string HoldSecondsLabel(string typeId) => typeId switch
    {
        "Compare.Debounce" => "Hold for (sec)",
        "Compare.StableFor" => "Stable for (sec)",
        "Compare.RateLimit" => "Interval (sec)",
        _ => "Seconds"
    };

    private static string HoldSecondsDefault(string typeId) => typeId switch
    {
        "Compare.RateLimit" => "1",
        _ => "0.5"
    };

    private static void AppendOutputFields(LogicNode node, List<LogicParamField> fields)
    {
        fields.Add(Branch("branch", "whenTrue"));

        if (node.typeId == "Action.ThenAfter")
        {
            fields.Add(Sec("delaySec", "Then after (sec)", "1"));
            fields.Add(Txt("nextNodeId", "Next block ID", ""));
        }
    }

    private static void AppendExpectValueFields(LogicNode node, List<LogicParamField> fields, LogicGraph? graph)
    {
        if (node.typeId is "Compare.InRange" or "Compare.OutsideRange")
        {
            fields.Add(Num("min", "Minimum (from)", "0"));
            fields.Add(Num("max", "Maximum (to)", "100"));
            return;
        }

        if (node.typeId is "Compare.Hysteresis")
        {
            fields.Add(Num("expectValue", "On threshold", "15", LogicParamSection.Check));
            return;
        }

        if (node.typeId is "Compare.Changed" or "Compare.IsTrue" or "Compare.IsFalse")
        {
            return;
        }

        var expectKind = GameBindingValueSchema.ResolveExpectValueKind(node, graph);
        if (expectKind == LogicParamKind.Bool)
        {
            fields.Add(ExpectBoolField(node));
            return;
        }

        if (expectKind == LogicParamKind.Text)
        {
            fields.Add(Txt("expectValue", "Value (equals)", node.parameters.GetValueOrDefault("expectValue") ?? string.Empty));
            return;
        }

        fields.Add(ThresholdField(node));
    }

    private static LogicParamField ExpectBoolField(LogicNode node)
    {
        var current = node.parameters.TryGetValue("expectValue", out var ev) && !string.IsNullOrWhiteSpace(ev)
            ? ev
            : GameBindingValueSchema.DefaultExpectValue(node, LogicParamKind.Bool);
        return Choice(
            "expectValue",
            "Expected value (true / false)",
            new[] { "true", "false" },
            current.Equals("false", StringComparison.OrdinalIgnoreCase) ? "false" : "true",
            LogicParamSection.Check);
    }

    private static LogicParamField ThresholdField(LogicNode node)
    {
        var (label, ph) = node.typeId switch
        {
            "Compare.GreaterThan" => ("Threshold (greater)", "15"),
            "Compare.GreaterOrEqual" => ("Threshold (≥)", "15"),
            "Compare.LessThan" => ("Threshold (less)", "100"),
            "Compare.LessOrEqual" => ("Threshold (≤)", "100"),
            "Compare.Equals" => ("Value (equals)", "0"),
            "Compare.NotEquals" => ("Value (not equal)", "0"),
            "Compare.CrossedAbove" => ("Threshold (crossed up)", "15"),
            "Compare.CrossedBelow" => ("Threshold (crossed down)", "15"),
            "Compare.Approximately" => ("Expected value", "0"),
            _ => ("Threshold", DefaultExpect(node))
        };

        return Num("expectValue", label, string.IsNullOrWhiteSpace(ph) ? DefaultExpect(node) : ph, LogicParamSection.Check);
    }

    private static void AppendFieldsForType(string typeId, List<LogicParamField> fields)
    {
        // Legacy nodes без kind — поля по typeId не добавляем (источник только через kind=source).
    }

    /// <summary>Идентификатор параметра для блока «Источник»: сам typeId блока.</summary>
    public static string SourceIdentity(LogicNode node) =>
        node.kind == "source" ? node.typeId : ResolveLegacySourceParam(node);

    private static string ResolveLegacySourceParam(LogicNode node) =>
        node.parameters.TryGetValue("sourceParam", out var sp) && !string.IsNullOrWhiteSpace(sp) ? sp
        : node.parameters.TryGetValue("gameTarget", out var gt) && !string.IsNullOrWhiteSpace(gt) ? gt
        : IsCatalogParamId(node.typeId) ? node.typeId
        : string.Empty;

    private static bool IsCatalogParamId(string typeId) =>
        typeId.StartsWith("Read.", StringComparison.Ordinal)
        || typeId.StartsWith("Write.", StringComparison.Ordinal)
        || typeId.StartsWith("UI.", StringComparison.Ordinal)
        || typeId.StartsWith("Telemetry.", StringComparison.Ordinal);

    private static bool NeedsHoldSeconds(string typeId) => typeId is
        "Compare.Debounce"
        or "Compare.StableFor"
        or "Compare.RateLimit";

    private static string DefaultExpect(LogicNode node) => node.typeId switch
    {
        "Compare.LessThan" or "Compare.LessOrEqual" => "100",
        "Compare.InRange" => "15",
        _ => "15"
    };

    public static Dictionary<string, string> GetDefaults(string typeId, string kind)
    {
        var node = new LogicNode { typeId = typeId, kind = kind };
        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in GetFields(node))
        {
            if (!string.IsNullOrWhiteSpace(field.Placeholder))
            {
                defaults[field.Key] = field.Placeholder;
            }
            else if (field.Kind == LogicParamKind.Bool)
            {
                defaults[field.Key] = "true";
            }
            else if (field.Kind == LogicParamKind.Choice && field.Choices?.Count > 0)
            {
                defaults[field.Key] = field.Choices[0];
            }
        }

        return defaults;
    }

    public static string FormatSummary(LogicNode node, DisplayLayerService display)
    {
        var parts = new List<string>();

        if (node.kind == "source")
        {
            parts.Add(display.Title(node.typeId));
        }
        else if (node.kind is "check" or "gate")
        {
            parts.Add(LogicCheckCatalog.Title(node.typeId));
            if (node.parameters.TryGetValue("watchParam", out var wp) && !string.IsNullOrWhiteSpace(wp))
            {
                parts.Add(display.Title(wp));
            }
        }
        else if (node.parameters.TryGetValue("watchParam", out var wp) && !string.IsNullOrWhiteSpace(wp))
        {
            parts.Add($"watch: {display.Title(wp)}");
        }
        else if (node.parameters.TryGetValue("sourceParam", out var sp) && !string.IsNullOrWhiteSpace(sp))
        {
            parts.Add(display.Title(sp));
        }

        if (node.parameters.TryGetValue("expectValue", out var ev) && !string.IsNullOrWhiteSpace(ev))
        {
            var expectKind = node.kind is "check" or "gate"
                ? GameBindingValueSchema.ResolveExpectValueKind(node)
                : LogicParamKind.Number;
            parts.Add($"expect: {GameBindingValueSchema.FormatExpectDisplay(ev, expectKind)}");
        }
        else if (node.parameters.TryGetValue("threshold", out var th) && !string.IsNullOrWhiteSpace(th))
        {
            parts.Add($"expect: {th}");
        }

        if (node.parameters.TryGetValue("branch", out var br))
        {
            parts.Add(LogicParamCatalog.BranchLabel(br));
        }

        if (node.parameters.TryGetValue("targetId", out var tid) && !string.IsNullOrWhiteSpace(tid))
        {
            parts.Add($"→ {tid}");
        }
        else if (node.parameters.TryGetValue("labelId", out var lid) && !string.IsNullOrWhiteSpace(lid))
        {
            parts.Add($"→ {lid}");
        }

        if (LogicOutputMemberWrite.IsEnabled(node)
            && !string.IsNullOrWhiteSpace(LogicOutputMemberWrite.GetBindingId(node)))
        {
            parts.Add($"write: {LogicOutputMemberWrite.GetValue(node)} → {LogicOutputMemberWrite.GetBindingId(node)}");
        }

        foreach (var ch in LogicOutputChangeCatalog.EnabledChanges(node))
        {
            var val = LogicOutputChangeCatalog.GetValue(node, ch.Id, ch.DefaultValue);
            parts.Add(ch.ValueKind switch
            {
                LogicParamKind.Color => $"{ch.Label}: {val}",
                LogicParamKind.Bool => $"{ch.Label}: {(val == "true" ? "yes" : "no")}",
                _ => $"{ch.Label}: {val}"
            });
        }

        if (node.parameters.TryGetValue("colorHtml", out var c) && !string.IsNullOrWhiteSpace(c)
            && !LogicOutputChangeCatalog.IsEnabled(node, "color"))
        {
            parts.Add(c);
        }

        if (node.parameters.TryGetValue("text", out var tx) && !string.IsNullOrWhiteSpace(tx))
        {
            parts.Add($"«{tx}»");
        }

        if (node.parameters.TryGetValue("holdSeconds", out var hs) && !string.IsNullOrWhiteSpace(hs) && hs != "0")
        {
            parts.Add($"after {hs}s");
        }

        if (parts.Count == 0 && node.parameters.TryGetValue("hint", out var hint) && !string.IsNullOrWhiteSpace(hint))
        {
            return hint;
        }

        return string.Join(" · ", parts);
    }

    public static void MergeDefaults(LogicNode node)
    {
        foreach (var kv in GetDefaults(node.typeId, node.kind))
        {
            if (!node.parameters.ContainsKey(kv.Key) || string.IsNullOrWhiteSpace(node.parameters[kv.Key]))
            {
                node.parameters[kv.Key] = kv.Value;
            }
        }

        SyncLegacyKeys(node);
    }

    public static void SyncLegacyKeys(LogicNode node)
    {
        if (node.kind == "output")
        {
            LogicOutputChangeCatalog.ImportLegacySingleAction(node);
        }

        if (node.parameters.TryGetValue("expectValue", out var ev) && !string.IsNullOrWhiteSpace(ev))
        {
            node.parameters["threshold"] = ev;
        }

        if (node.kind is "check" or "gate"
            && node.parameters.TryGetValue("watchParam", out var wp) && !string.IsNullOrWhiteSpace(wp))
        {
            node.parameters["sourceParam"] = wp;
            node.parameters["gameTarget"] = wp;
        }

        if (node.parameters.TryGetValue("onThreshold", out var on) && !string.IsNullOrWhiteSpace(on))
        {
            node.parameters["threshold"] = on;
        }

        if (node.parameters.TryGetValue("targetId", out var tid) && !string.IsNullOrWhiteSpace(tid))
        {
            node.parameters["labelId"] = tid;
            node.parameters["instanceId"] = tid;
        }
        else if (node.kind == "output")
        {
            var upstream = ResolveLegacySourceParam(node);
            if (!string.IsNullOrWhiteSpace(upstream))
            {
                var inferred = LogicOutputChangeCatalog.InferWriteTarget(upstream);
                if (!string.IsNullOrWhiteSpace(inferred))
                {
                    node.parameters["targetId"] = inferred;
                    node.parameters["labelId"] = inferred;
                }
            }
        }

        if (LogicOutputChangeCatalog.IsEnabled(node, "color"))
        {
            node.parameters["colorHtml"] = LogicOutputChangeCatalog.GetValue(node, "color", "#FF4400");
        }

        if (LogicOutputChangeCatalog.IsEnabled(node, "text"))
        {
            node.parameters["text"] = LogicOutputChangeCatalog.GetValue(node, "text", "");
        }

        if (LogicOutputChangeCatalog.IsEnabled(node, "format"))
        {
            node.parameters["formatString"] = LogicOutputChangeCatalog.GetValue(node, "format", "{0:F0}");
        }

        if (LogicOutputChangeCatalog.IsEnabled(node, "visible"))
        {
            node.parameters["visible"] = LogicOutputChangeCatalog.GetValue(node, "visible", "true");
        }

        if (LogicOutputChangeCatalog.IsEnabled(node, "sound"))
        {
            node.parameters["clipName"] = LogicOutputChangeCatalog.GetValue(node, "sound", "");
        }

        if (node.kind == "source")
        {
            SyncSourceIdentity(node);
        }

        if (node.parameters.TryGetValue("holdSeconds", out var hs) && !string.IsNullOrWhiteSpace(hs))
        {
            node.parameters["seconds"] = hs;
            node.parameters["delaySec"] = hs;
            node.parameters["intervalSec"] = hs;
        }
    }

    private static void SyncSourceIdentity(LogicNode node)
    {
        if (string.IsNullOrWhiteSpace(node.typeId))
        {
            return;
        }

        node.parameters["sourceParam"] = node.typeId;
        node.parameters["gameTarget"] = node.typeId;
    }

    public static string SectionTitle(LogicParamSection section) => section switch
    {
        LogicParamSection.Source => "① SOURCE",
        LogicParamSection.Check => "② CHECK — parameter & threshold",
        LogicParamSection.Output => "③ OUTPUT — if yes / if no",
        _ => section.ToString()
    };

    private static LogicParamField Field(string key, LogicParamKind kind, string label, LogicParamSection section, string? ph = null) =>
        new(key, kind, label, section, ph);

    private static LogicParamField Branch(string key, string defaultBranch) =>
        new(key, LogicParamKind.Choice, "When it fires", LogicParamSection.Output, defaultBranch, LogicParamCatalog.BranchChoices);

    private static LogicParamField Choice(string key, string label, IReadOnlyList<string> choices, string defaultChoice, LogicParamSection section) =>
        new(key, LogicParamKind.Choice, label, section, defaultChoice, choices);

    private static LogicParamField Exp(string key, string ph) =>
        Num(key, "Expected value", ph, LogicParamSection.Check);

    private static LogicParamField Num(string key, string label, string ph, LogicParamSection section = LogicParamSection.Check) =>
        Field(key, LogicParamKind.Number, label, section, ph);

    private static LogicParamField Sec(string key, string label, string ph) =>
        Field(key, LogicParamKind.Number, label, LogicParamSection.Check, ph);

    private static LogicParamField Txt(string key, string label, string ph, LogicParamSection section = LogicParamSection.Output) =>
        Field(key, LogicParamKind.Text, label, section, ph);

    private static LogicParamField Bind(string key, string label) =>
        Field(key, LogicParamKind.Binding, label, LogicParamSection.Source, "Cockpit.velocity");

    private static LogicParamField Color(string key, string ph) =>
        Field(key, LogicParamKind.Color, "Color", LogicParamSection.Output, ph);

    private static LogicParamField Font(string key, string ph) =>
        Num(key, "Font size", ph, LogicParamSection.Output);

    private static LogicParamField Vol(string key, string ph) =>
        Num(key, "Volume", ph, LogicParamSection.Output);

    private static LogicParamField Bool(string key, string label, string ph) =>
        Field(key, LogicParamKind.Bool, label, LogicParamSection.Output, ph);
}
