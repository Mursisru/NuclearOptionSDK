using System.Text;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public static class LogicOutputPreviewBuilder
{
    public static string BuildGraphModPreview(LogicProject project) =>
        LogicModSourceGenerator.Generate(project);

    public static string BuildNodePreview(LogicNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// {node.kind.ToUpperInvariant()} · {node.typeId}");
        sb.AppendLine();

        switch (node.kind)
        {
            case "source":
                AppendSource(sb, node);
                break;
            case "check":
            case "gate":
                AppendCheck(sb, node);
                break;
            case "output":
                AppendOutput(sb, node);
                break;
            default:
                sb.AppendLine($"// typeId: {node.typeId}");
                break;
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendSource(StringBuilder sb, LogicNode node)
    {
        var param = node.kind == "source" ? node.typeId : ResolveParam(node, "sourceParam");
        if (string.IsNullOrWhiteSpace(param))
        {
            sb.AppendLine("// Drag a parameter from Game Code → Parameters (Read.* / Member.Bind)");
            return;
        }

        if (NoGameParameterCatalog.TryGet(param, out var entry))
        {
            sb.AppendLine($"// {entry.Description}");
            sb.AppendLine($"float value = /* read */ {entry.GamePath};");
        }
        else
        {
            sb.AppendLine($"var value = Read(\"{param}\");");
        }
    }

    private static void AppendCheck(StringBuilder sb, LogicNode node)
    {
        sb.AppendLine($"// {LogicCheckCatalog.Title(node.typeId)}");
        sb.AppendLine($"// {LogicCheckCatalog.Hint(node.typeId)}");

        var param = ResolveWatchParam(node);
        if (string.IsNullOrWhiteSpace(param))
        {
            sb.AppendLine("// watchParam: select parameter on the right");
            sb.AppendLine("bool ok = false;");
            return;
        }

        if (NoGameParameterCatalog.TryGet(param, out var entry))
        {
            sb.AppendLine($"// {entry.GamePath}");
            sb.AppendLine($"float value = /* read */ {entry.GamePath};");
        }
        else
        {
            sb.AppendLine($"float value = Read(\"{param}\");");
        }

        sb.AppendLine();

        if (node.parameters.TryGetValue("expectValue", out var expect) && !string.IsNullOrWhiteSpace(expect))
        {
            sb.AppendLine($"bool ok = value {CompareOp(node.typeId)} {expect};");
        }
        else if (node.parameters.TryGetValue("min", out var min) && node.parameters.TryGetValue("max", out var max))
        {
            sb.AppendLine(node.typeId == "Compare.OutsideRange"
                ? $"bool ok = value < {min} || value > {max};"
                : $"bool ok = value >= {min} && value <= {max};");
        }
        else if (node.typeId is "Compare.IsTrue")
        {
            sb.AppendLine("bool ok = value != 0;");
        }
        else if (node.typeId is "Compare.IsFalse")
        {
            sb.AppendLine("bool ok = value == 0;");
        }
        else if (node.typeId is "Compare.Changed")
        {
            sb.AppendLine("bool ok = value != previousValue;");
        }
        else
        {
            sb.AppendLine("bool ok = EvaluateCompare(value);");
        }
    }

    private static string ResolveWatchParam(LogicNode node)
    {
        if (node.parameters.TryGetValue("watchParam", out var wp) && !string.IsNullOrWhiteSpace(wp))
        {
            return wp;
        }

        return ResolveParam(node, "sourceParam");
    }

    private static void AppendOutput(StringBuilder sb, LogicNode node)
    {
        var branch = node.parameters.TryGetValue("branch", out var br) ? br : "whenTrue";
        sb.AppendLine($"if ({BranchCode(branch)}) {{");

        var target = ResolveOutputTarget(node);
        sb.AppendLine($"  // Target from Source chain: {target}");
        sb.AppendLine();

        if (LogicOutputMemberWrite.IsEnabled(node)
            && !string.IsNullOrWhiteSpace(LogicOutputMemberWrite.GetBindingId(node)))
        {
            var bid = LogicOutputMemberWrite.GetBindingId(node)!;
            var val = LogicOutputMemberWrite.GetValue(node);
            sb.AppendLine($"  WriteMember(\"{bid}\", {FormatLiteral(val, LogicParamKind.Text)});");
        }

        foreach (var change in LogicOutputChangeCatalog.EnabledChanges(node))
        {
            AppendChangeLine(sb, change, node, target);
        }

        if (!LogicOutputChangeCatalog.EnabledChanges(node).Any()
            && !LogicOutputMemberWrite.IsEnabled(node))
        {
            sb.AppendLine("  // Check items under What to change below");
        }

        sb.AppendLine("}");
    }

    private static void AppendChangeLine(StringBuilder sb, OutputChangeDef change, LogicNode node, string target)
    {
        var val = LogicOutputChangeCatalog.GetValue(node, change.Id, change.DefaultValue);
        if (change.Id.StartsWith("Member.", StringComparison.Ordinal))
        {
            sb.AppendLine($"  WriteMember(\"{change.Id}\", {FormatLiteral(val, change.ValueKind)});");
            return;
        }

        if (NoGameParameterCatalog.TryGet(change.Id, out var entry))
        {
            sb.AppendLine($"  {entry.GamePath} = {FormatLiteral(val, entry.ValueType)};");
            return;
        }

        sb.AppendLine($"  // {change.Label}");
        sb.AppendLine($"  Apply(\"{target}\", \"{change.Id}\", {FormatLiteral(val, change.ValueKind)});");
    }

    private static string ResolveOutputTarget(LogicNode node)
    {
        if (node.parameters.TryGetValue("targetId", out var tid) && !string.IsNullOrWhiteSpace(tid))
        {
            return tid;
        }

        if (node.parameters.TryGetValue("labelId", out var lid) && !string.IsNullOrWhiteSpace(lid))
        {
            return lid;
        }

        var source = ResolveParam(node, "sourceParam");
        if (!string.IsNullOrWhiteSpace(source))
        {
            return LogicOutputChangeCatalog.InferWriteTarget(source);
        }

        return "/* source from graph */";
    }

    private static string ResolveParam(LogicNode node, string key)
    {
        if (node.parameters.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
        {
            return v;
        }

        return node.typeId.StartsWith("Read.", StringComparison.Ordinal)
            || node.typeId.StartsWith("Write.", StringComparison.Ordinal)
            || node.typeId.StartsWith("UI.", StringComparison.Ordinal)
            || node.typeId.StartsWith("Telemetry.", StringComparison.Ordinal)
            ? node.typeId
            : string.Empty;
    }

    private static string CompareOp(string typeId) => typeId switch
    {
        "Compare.LessThan" => "<",
        "Compare.LessOrEqual" => "<=",
        "Compare.GreaterThan" => ">",
        "Compare.GreaterOrEqual" => ">=",
        "Compare.Equals" or "Compare.Approximately" => "==",
        "Compare.NotEquals" => "!=",
        _ => "? "
    };

    private static string BranchCode(string branch) =>
        branch == "whenFalse" ? "/* condition NOT met */" : "/* condition met */";

    private static string FormatLiteral(string value, string valueType) =>
        valueType switch
        {
            "float" or "double" or "int" => double.TryParse(value, out _) ? value : $"\"{value}\"",
            "bool" => value is "true" or "false" ? value : $"\"{value}\"",
            _ => $"\"{value}\""
        };

    private static string FormatLiteral(string value, LogicParamKind kind) => kind switch
    {
        LogicParamKind.Number => double.TryParse(value, out _) ? value : $"\"{value}\"",
        LogicParamKind.Bool => value is "true" or "false" ? value : $"\"{value}\"",
        _ => $"\"{value}\""
    };
}
