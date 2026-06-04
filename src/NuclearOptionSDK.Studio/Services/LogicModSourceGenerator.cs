using System.Globalization;
using System.Text;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Full BepInEx mod C# source from a logic graph (preview + build).</summary>
public static class LogicModSourceGenerator
{
    public static string Generate(
        LogicProject project,
        string modName = "LogicMod",
        string pluginGuid = "com.nuclearstudio.logicmod")
    {
        var graph = project.userGraph ?? new LogicGraph();
        PrepareGraphForCodegen(graph);
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------------");
        sb.AppendLine($"// Nuclear Studio — generated logic mod (preview / build)");
        sb.AppendLine($"// Project: {project.name} · merge={project.mergeMode} · tick={project.tickRateHz} Hz");
        sb.AppendLine($"// Graph: {graph.nodes.Length} node(s), {graph.edges.Length} edge(s)");
        if (graph.nodes.Length > 0)
        {
            var nodeDigest = string.Join(",", graph.nodes.Select(n => $"{n.id}:{n.typeId}"));
            sb.AppendLine($"// Nodes: {nodeDigest}");
        }

        sb.AppendLine("// -----------------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using BepInEx;");
        sb.AppendLine("using HarmonyLib;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine($"namespace {modName}_Engine;");
        sb.AppendLine();
        sb.AppendLine($"[BepInPlugin(\"{pluginGuid}\", \"{modName}\", \"1.0.0\")]");
        sb.AppendLine($"public sealed class {modName}Plugin : BaseUnityPlugin");
        sb.AppendLine("{");
        sb.AppendLine("    private void Awake()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var go = new GameObject(\"{modName}_Runtime\");");
        sb.AppendLine("        DontDestroyOnLoad(go);");
        sb.AppendLine($"        go.AddComponent<{modName}Runtime>();");
        if (UsePatchMode(project))
        {
            sb.AppendLine($"        new Harmony(\"{pluginGuid}.logic\").PatchAll(typeof({modName}Patch));");
        }
        sb.AppendLine($"        Logger.LogInfo(\"{modName} loaded (Nuclear Studio).\");");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        if (UsePatchMode(project))
        {
            EmitPatchClass(sb, project, modName);
            sb.AppendLine();
        }
        sb.AppendLine($"public sealed class {modName}Runtime : MonoBehaviour");
        sb.AppendLine("{");
        sb.AppendLine($"    private static {modName}Runtime? _instance;");
        sb.AppendLine("    private static bool _forceTick;");
        sb.AppendLine("    private readonly Dictionary<string, double> _prevNumeric = new(StringComparer.Ordinal);");
        sb.AppendLine("    private float _accum;");
        sb.AppendLine("    private float _nextContextRetryAt;");
        sb.AppendLine("    private int _guardDrops;");
        sb.AppendLine("    private int _runtimeErrors;");
        sb.AppendLine("    private bool _runtimeReady;");
        sb.AppendLine();
        var tickInterval = (1f / Math.Max(1f, (float)project.tickRateHz)).ToString("F3", CultureInfo.InvariantCulture);
        sb.AppendLine($"    private const float TickInterval = {tickInterval}f;");
        sb.AppendLine($"    private const bool DiagnosticEnabled = {(project.diagnosticMode ? "true" : "false")};");
        sb.AppendLine();
        sb.AppendLine("    private void Awake()");
        sb.AppendLine("    {");
        sb.AppendLine("        _instance = this;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static void TriggerImmediateTick()");
        sb.AppendLine("    {");
        sb.AppendLine("        _forceTick = true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void Update()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_nextContextRetryAt > 0f && Time.unscaledTime < _nextContextRetryAt)");
        sb.AppendLine("            return;");
        sb.AppendLine("        _accum += Time.unscaledDeltaTime;");
        sb.AppendLine("        if (!_forceTick && _accum < TickInterval)");
        sb.AppendLine("            return;");
        sb.AppendLine("        _forceTick = false;");
        sb.AppendLine("        _accum = 0f;");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            if (!TryEnsureRuntimeContext())");
        sb.AppendLine("                return;");
        BindingCodegen.EmitLocalAircraftGuard(sb, "            ");
        sb.AppendLine();
        sb.AppendLine("            EvaluateUserGraph(aircraft);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            _runtimeErrors++;");
        sb.AppendLine("            _nextContextRetryAt = Time.unscaledTime + 0.5f;");
        sb.AppendLine("            Diag($\"runtime-error #{_runtimeErrors}: {ex.GetType().Name} {ex.Message}\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private void EvaluateUserGraph(Aircraft aircraft)");
        sb.AppendLine("    {");

        if (graph.nodes.Length == 0)
        {
            sb.AppendLine("        // Empty graph — add Source → Check → Output blocks.");
        }
        else
        {
            if (graph.edges.Length > 0)
            {
                var edgeDigest = string.Join(", ", graph.edges.Select(e => $"{e.fromNode}->{e.toNode}"));
                sb.AppendLine($"        // Edges: {edgeDigest}");
            }

            EmitGraph(sb, graph);
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        AppendGraphRuntimeHelpers(sb);
        sb.AppendLine("}");

        return sb.ToString().TrimEnd();
    }

    private static void EmitGraph(StringBuilder sb, LogicGraph graph)
    {
        var nodeMap = graph.nodes.ToDictionary(n => n.id, StringComparer.Ordinal);
        var outgoing = BuildOutgoing(graph.edges);
        var sources = graph.nodes.Where(n => n.kind == "source").ToList();

        if (sources.Count == 0)
        {
            sb.AppendLine("        // No source nodes — drag a field from Game Code onto the graph.");
            return;
        }

        var chainIndex = 0;
        foreach (var source in sources)
        {
            chainIndex++;
            sb.AppendLine($"        // --- Chain {chainIndex}: source [{source.id}] {DescribeNode(source)} ---");

            var binding = ResolveSourceBinding(source);
            if (string.IsNullOrWhiteSpace(binding))
            {
                sb.AppendLine("        // (source has no binding — connect Member.Bind or Telemetry)");
                sb.AppendLine();
                continue;
            }

            var targets = GetTargets(source.id, outgoing);
            if (targets.Count == 0)
            {
                sb.AppendLine($"        // Read only: {binding}");
                EmitReadComment(sb, source, binding);
                sb.AppendLine();
                continue;
            }

            foreach (var targetId in targets)
            {
                EmitFromNode(sb, graph, nodeMap, outgoing, targetId, source, binding, indent: "        ");
            }

            sb.AppendLine();
        }
    }

    private static void EmitFromNode(
        StringBuilder sb,
        LogicGraph graph,
        Dictionary<string, LogicNode> nodeMap,
        Dictionary<string, List<string>> outgoing,
        string nodeId,
        LogicNode source,
        string sourceBinding,
        string indent)
    {
        if (!nodeMap.TryGetValue(nodeId, out var node))
        {
            return;
        }

        switch (node.kind)
        {
            case "check":
            case "gate":
                EmitCheckBlock(sb, node, graph, sourceBinding, indent);
                var checkTargets = GetTargets(nodeId, outgoing);
                if (checkTargets.Count == 0)
                {
                    sb.AppendLine($"{indent}    // Connect an Output block to this check (wire check → output).");
                }

                foreach (var next in checkTargets)
                {
                    EmitFromNode(sb, graph, nodeMap, outgoing, next, source, sourceBinding, indent + "    ");
                }

                sb.AppendLine($"{indent}}}");
                break;

            case "output":
                EmitOutputBlock(sb, node, graph, indent);
                break;

            case "merge":
                sb.AppendLine($"{indent}// merge [{node.id}] mergeMode handled at project level");
                foreach (var next in GetTargets(nodeId, outgoing))
                {
                    EmitFromNode(sb, graph, nodeMap, outgoing, next, source, sourceBinding, indent);
                }

                break;

            default:
                foreach (var next in GetTargets(nodeId, outgoing))
                {
                    EmitFromNode(sb, graph, nodeMap, outgoing, next, source, sourceBinding, indent);
                }

                break;
        }
    }

    private static void PrepareGraphForCodegen(LogicGraph graph)
    {
        foreach (var node in graph.nodes)
        {
            if (node.kind == "output")
            {
                LogicNodeParameterSchema.SyncLegacyKeys(node);
            }
        }
    }

    private static void EmitCheckBlock(StringBuilder sb, LogicNode check, LogicGraph graph, string fallbackBinding, string indent)
    {
        var binding = ResolveCheckWatchBinding(check, graph, fallbackBinding);
        if (string.IsNullOrWhiteSpace(binding))
        {
            sb.AppendLine($"{indent}// Check [{check.id}]: missing watchParam/binding — skipped");
            sb.AppendLine($"{indent}Diag(\"check:{check.id}:missing-binding\");");
            sb.AppendLine($"{indent}if (false)");
            sb.AppendLine($"{indent}{{");
            return;
        }
        var clr = GameBindingValueSchema.ResolveClrType(check, graph);
        var kind = GameBindingValueSchema.ClassifyClrType(clr);
        var title = LogicCheckCatalog.Title(check.typeId);
        var valueVar = CheckValueVar(check);
        var okVar = CheckOkVar(check);

        sb.AppendLine($"{indent}// Check [{check.id}]: {title}");
        sb.AppendLine($"{indent}Diag(\"check:{check.id}:{check.typeId}\");");

        if (check.typeId is "Gate.WhileAirborne" or "Gate.WhileOnGround")
        {
            EmitRadarAltGate(sb, check, graph, indent, valueVar, okVar);
            sb.AppendLine($"{indent}if ({okVar})");
            sb.AppendLine($"{indent}{{");
            return;
        }

        if (check.typeId == "Gate.OnlyWhenInFlight")
        {
            var speedBinding = ResolveWatchParam(check)
                               ?? LogicParamCatalog.ResolveDefaultWatchBindingForCheck(check.typeId)
                               ?? binding;
            var velocityThreshold = check.parameters.TryGetValue("threshold", out var vt) && !string.IsNullOrWhiteSpace(vt)
                ? vt
                : "10";
            BindingCodegen.EmitReadFloat(sb, speedBinding, indent, valueVar);
            sb.AppendLine($"{indent}bool {okVar} = {valueVar} > {velocityThreshold}f;");
            sb.AppendLine($"{indent}if ({okVar})");
            sb.AppendLine($"{indent}{{");
            return;
        }

        if (check.typeId == "Compare.Changed")
        {
            BindingCodegen.EmitReadFloat(sb, binding, indent, valueVar);
            sb.AppendLine($"{indent}bool {okVar} = EvaluateChanged(\"{check.id}\", {valueVar});");
            sb.AppendLine($"{indent}if ({okVar})");
            sb.AppendLine($"{indent}{{");
            return;
        }

        if (check.typeId == "Compare.IsTrue")
        {
            BindingCodegen.EmitReadFloat(sb, binding, indent, valueVar);
            sb.AppendLine($"{indent}bool {okVar} = {valueVar} > 0f;");
            sb.AppendLine($"{indent}if ({okVar})");
            sb.AppendLine($"{indent}{{");
            return;
        }

        if (check.typeId == "Compare.IsFalse")
        {
            BindingCodegen.EmitReadFloat(sb, binding, indent, valueVar);
            sb.AppendLine($"{indent}bool {okVar} = {valueVar} == 0f;");
            sb.AppendLine($"{indent}if ({okVar})");
            sb.AppendLine($"{indent}{{");
            return;
        }

        if (kind == LogicParamKind.Bool)
        {
            BindingCodegen.EmitReadBool(sb, binding, indent, valueVar);
            var expect = check.parameters.TryGetValue("expectValue", out var ev) && !string.IsNullOrWhiteSpace(ev)
                ? ev
                : "true";
            sb.AppendLine($"{indent}bool {okVar} = {valueVar} == {expect.ToLowerInvariant()};");
        }
        else
        {
            BindingCodegen.EmitReadFloat(sb, binding, indent, valueVar);
            EmitCompareExpression(sb, check, indent, valueVar, okVar);
        }

        sb.AppendLine($"{indent}if ({okVar})");
        sb.AppendLine($"{indent}{{");
    }

    private static string CheckValueVar(LogicNode check) => $"value_{SanitizeCheckId(check.id)}";

    private static string CheckOkVar(LogicNode check) => $"ok_{SanitizeCheckId(check.id)}";

    private static string SanitizeCheckId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "node";
        }

        var chars = id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var sanitized = new string(chars);
        return char.IsDigit(sanitized[0]) ? "n_" + sanitized : sanitized;
    }

    private static void EmitRadarAltGate(
        StringBuilder sb,
        LogicNode check,
        LogicGraph graph,
        string indent,
        string valueVar,
        string okVar)
    {
        var altBinding = ResolveWatchParam(check)
                         ?? LogicParamCatalog.ResolveDefaultWatchBindingForCheck(check.typeId)
                         ?? LogicParamCatalog.ResolveUpstreamWatchParam(check, graph);
        if (string.IsNullOrWhiteSpace(altBinding))
        {
            sb.AppendLine($"{indent}Diag(\"check:{check.id}:missing-binding\");");
            sb.AppendLine($"{indent}bool {okVar} = false;");
            return;
        }

        var threshold = check.parameters.TryGetValue("threshold", out var th) && !string.IsNullOrWhiteSpace(th)
            ? th
            : check.typeId == "Gate.WhileOnGround" ? "1" : "1";

        BindingCodegen.EmitReadFloat(sb, altBinding, indent, valueVar);
        if (check.typeId == "Gate.WhileOnGround")
        {
            sb.AppendLine($"{indent}bool {okVar} = {valueVar} <= {threshold}f;");
        }
        else
        {
            sb.AppendLine($"{indent}bool {okVar} = {valueVar} > {threshold}f;");
        }
    }

    private static void EmitCompareExpression(StringBuilder sb, LogicNode check, string indent, string valueVar, string okVar)
    {
        var threshold = check.parameters.TryGetValue("expectValue", out var ev) && !string.IsNullOrWhiteSpace(ev)
            ? ev
            : check.parameters.TryGetValue("threshold", out var th) ? th : "0";

        var op = check.typeId switch
        {
            "Compare.LessThan" => "<",
            "Compare.LessOrEqual" => "<=",
            "Compare.GreaterThan" => ">",
            "Compare.GreaterOrEqual" => ">=",
            "Compare.Equals" or "Compare.Approximately" => "==",
            "Compare.NotEquals" => "!=",
            "Compare.IsTrue" => ">",
            "Compare.IsFalse" => "==",
            _ => ">"
        };

        if (check.typeId is "Compare.InRange" or "Compare.OutsideRange")
        {
            var min = check.parameters.GetValueOrDefault("min") ?? "0";
            var max = check.parameters.GetValueOrDefault("max") ?? "100";
            if (check.typeId == "Compare.OutsideRange")
            {
                sb.AppendLine($"{indent}bool {okVar} = {valueVar} < {min}f || {valueVar} > {max}f;");
            }
            else
            {
                sb.AppendLine($"{indent}bool {okVar} = {valueVar} >= {min}f && {valueVar} <= {max}f;");
            }

            return;
        }

        if (check.typeId == "Compare.Changed")
        {
            sb.AppendLine($"{indent}bool {okVar} = EvaluateChanged(\"{check.id}\", {valueVar});");
            return;
        }

        sb.AppendLine($"{indent}bool {okVar} = {valueVar} {op} {threshold}f;");
    }

    private static void EmitOutputBlock(StringBuilder sb, LogicNode output, LogicGraph graph, string indent)
    {
        var branch = output.parameters.TryGetValue("branch", out var br) ? br : "whenTrue";
        sb.AppendLine($"{indent}// Output [{output.id}] when {branch}");
        sb.AppendLine($"{indent}Diag(\"output:{output.id}:{output.typeId}\");");
        sb.AppendLine($"{indent}{{");
        var bodyStart = sb.Length;

        if (LogicOutputMemberWrite.IsEnabled(output))
        {
            var bid = LogicOutputMemberWrite.GetBindingId(output)
                      ?? LogicParamCatalog.ResolveUpstreamWatchParam(output, graph);
            if (!string.IsNullOrWhiteSpace(bid))
            {
                var clr = GameBindingValueSchema.ResolveClrType(output, graph)
                          ?? GameCodeIndexCache.TryGetClrType(bid);
                var kind = GameBindingValueSchema.ClassifyClrType(clr);
                var val = ResolveOutputValue(
                    LogicOutputMemberWrite.GetValue(output),
                    kind == LogicParamKind.Bool ? "false" : kind == LogicParamKind.Number ? "0" : string.Empty,
                    kind);
                EmitWriteMemberLine(sb, bid, val, clr, kind, indent + "    ");
            }
        }

        foreach (var change in LogicOutputChangeCatalog.EnabledChanges(output))
        {
            EmitOutputChange(sb, change, output, graph, indent + "    ");
        }

        if (sb.Length == bodyStart)
        {
            EmitLegacyOutputByTypeId(sb, output, indent + "    ");
        }

        if (sb.Length == bodyStart)
        {
            sb.AppendLine($"{indent}    // Enable writes in the Output inspector");
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void EmitLegacyOutputByTypeId(StringBuilder sb, LogicNode output, string indent)
    {
        var color = output.parameters.TryGetValue("colorHtml", out var html) && !string.IsNullOrWhiteSpace(html)
            ? html
            : LogicOutputChangeCatalog.GetValue(output, "color", "#FF4400");

        switch (output.typeId)
        {
            case "Action.SetHudColor":
            case "Action.SetOverlayColor":
                sb.AppendLine($"{indent}// HUD overlay color → {color}");
                break;
            case "Action.SetHudActive":
            case "Action.SetOverlayVisible":
                var visible = output.parameters.GetValueOrDefault("visible") ?? "true";
                sb.AppendLine($"{indent}// HUD visibility → {visible}");
                break;
            case "Action.SetHudText":
            case "Action.CreateOverlayLabel":
                var text = output.parameters.GetValueOrDefault("text") ?? "AoA";
                sb.AppendLine($"{indent}// HUD text → \"{EscapeString(text)}\"");
                break;
            case "Action.SetFontSize":
                sb.AppendLine($"{indent}// HUD font size → {output.parameters.GetValueOrDefault("fontSize") ?? "14"}");
                break;
            case "Action.PlaySound":
            case "Audio.Action.PlayClip":
                sb.AppendLine($"{indent}// PlaySound(\"{EscapeString(output.parameters.GetValueOrDefault("clipName") ?? "clip")}\")");
                break;
        }
    }

    private static void EmitOutputChange(StringBuilder sb, OutputChangeDef change, LogicNode node, LogicGraph graph, string indent)
    {
        var val = ResolveOutputValue(
            LogicOutputChangeCatalog.GetValue(node, change.Id, change.DefaultValue),
            change.DefaultValue,
            change.ValueKind);

        if (change.Id.StartsWith("Member.", StringComparison.Ordinal))
        {
            var clr = ResolveMemberChangeClrType(change.Id, node);
            var kind = GameBindingValueSchema.ClassifyClrType(clr);
            if (kind == LogicParamKind.Number
                && (val.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || val.Equals("false", StringComparison.OrdinalIgnoreCase)))
            {
                kind = LogicParamKind.Bool;
            }

            EmitWriteMemberLine(sb, change.Id, val, clr, kind, indent);
            return;
        }

        if (NoGameParameterCatalog.TryGet(change.Id, out var entry) && !string.IsNullOrWhiteSpace(entry.GamePath))
        {
            sb.AppendLine($"{indent}// {change.Label}: {entry.GamePath} = {FormatCSharpLiteral(val, entry.ValueType)}");
            sb.AppendLine($"{indent}// (wire to game API — path from catalog)");
            return;
        }

        switch (change.Id)
        {
            case "color":
                sb.AppendLine($"{indent}// HUD overlay color → {val}");
                break;
            case "visible":
                sb.AppendLine($"{indent}// HUD visibility → {val}");
                break;
            case "text":
                sb.AppendLine($"{indent}// HUD text → \"{EscapeString(val)}\"");
                break;
            case "sound":
                sb.AppendLine($"{indent}// PlaySound(\"{EscapeString(val)}\")");
                break;
            default:
                sb.AppendLine($"{indent}// {change.Label} → {val}");
                break;
        }
    }

    private static void EmitReadComment(StringBuilder sb, LogicNode source, string binding)
    {
        if (NoGameParameterCatalog.TryGet(binding, out var entry))
        {
            sb.AppendLine($"        // float value = /* {entry.GamePath} */;");
        }
        else
        {
            sb.AppendLine($"        // float value = GameBindingRuntime.ReadFloat(aircraft, \"{binding}\");");
        }
    }

    private static void AppendGraphRuntimeHelpers(StringBuilder sb)
    {
        sb.AppendLine("    private bool TryEnsureRuntimeContext()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_runtimeReady)");
        sb.AppendLine("            return true;");
        sb.AppendLine("        if (!Application.isPlaying)");
        sb.AppendLine("        {");
        sb.AppendLine("            _guardDrops++;");
        sb.AppendLine("            _nextContextRetryAt = Time.unscaledTime + 0.5f;");
        sb.AppendLine("            Diag($\"context-wait drops={_guardDrops}\");");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine("        _runtimeReady = true;");
        sb.AppendLine("        Diag(\"context-ready\");");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void Diag(string message)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (!DiagnosticEnabled)");
        sb.AppendLine("            return;");
        sb.AppendLine("        Debug.Log(\"[SDK-DIAG] \" + message);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool EvaluateChanged(string nodeId, float value)");
        sb.AppendLine("    {");
        sb.AppendLine("        var prev = _prevNumeric.TryGetValue(nodeId, out var p) ? p : value;");
        sb.AppendLine("        _prevNumeric[nodeId] = value;");
        sb.AppendLine("        return Math.Abs(value - prev) > 1e-6f;");
        sb.AppendLine("    }");
    }

    private static bool UsePatchMode(LogicProject project) =>
        string.Equals(project.executionMode, "Patch", StringComparison.OrdinalIgnoreCase);

    private static void EmitPatchClass(StringBuilder sb, LogicProject project, string modName)
    {
        var targetType = string.IsNullOrWhiteSpace(project.patchTargetType) ? "Aircraft" : project.patchTargetType.Trim();
        var targetMethod = string.IsNullOrWhiteSpace(project.patchMethodName) ? "Update" : project.patchMethodName.Trim();
        var patchKind = string.Equals(project.patchKind, "Postfix", StringComparison.OrdinalIgnoreCase) ? "Postfix" : "Prefix";
        sb.AppendLine($"[HarmonyPatch(typeof({targetType}), \"{targetMethod}\")]");
        sb.AppendLine($"public static class {modName}Patch");
        sb.AppendLine("{");
        sb.AppendLine($"    public static void {patchKind}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        {modName}Runtime.TriggerImmediateTick();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static string ResolveSourceBinding(LogicNode source)
    {
        if (source.parameters.TryGetValue("bindingId", out var bid) && !string.IsNullOrWhiteSpace(bid))
        {
            return bid;
        }

        if (source.typeId.StartsWith("Read.", StringComparison.Ordinal)
            || source.typeId.StartsWith("Telemetry.", StringComparison.Ordinal)
            || source.typeId.StartsWith("Member.", StringComparison.Ordinal))
        {
            return source.typeId;
        }

        return string.Empty;
    }

    private static string? ResolveWatchParam(LogicNode node)
    {
        if (node.parameters.TryGetValue("watchParam", out var wp) && !string.IsNullOrWhiteSpace(wp))
        {
            return wp;
        }

        return null;
    }

    private static string ResolveCheckWatchBinding(LogicNode check, LogicGraph graph, string fallbackBinding) =>
        ResolveWatchParam(check)
        ?? LogicParamCatalog.ResolveUpstreamWatchParam(check, graph)
        ?? LogicParamCatalog.ResolveDefaultWatchBindingForCheck(check.typeId)
        ?? fallbackBinding;

    private static string DescribeNode(LogicNode node) =>
        string.IsNullOrWhiteSpace(node.typeId) ? node.kind : $"{node.kind} · {node.typeId}";

    private static Dictionary<string, List<string>> BuildOutgoing(LogicEdge[] edges)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!map.TryGetValue(edge.fromNode, out var list))
            {
                list = new List<string>();
                map[edge.fromNode] = list;
            }

            list.Add(edge.toNode);
        }

        return map;
    }

    private static List<string> GetTargets(string nodeId, Dictionary<string, List<string>> outgoing) =>
        outgoing.TryGetValue(nodeId, out var list) ? list : new List<string>();

    private static string FormatCSharpLiteral(string value, string? clrType)
    {
        var kind = GameBindingValueSchema.ClassifyClrType(clrType);
        return FormatCSharpLiteral(value, kind);
    }

    private static string? ResolveMemberChangeClrType(string bindingId, LogicNode node)
    {
        if (node.parameters.TryGetValue(GameBindingValueSchema.ClrTypeParameterKey, out var nodeClr)
            && !string.IsNullOrWhiteSpace(nodeClr))
        {
            return nodeClr.Trim();
        }

        return GameCodeIndexCache.TryGetClrType(bindingId);
    }

    private static string ResolveOutputValue(string raw, string defaultValue, LogicParamKind kind)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Trim();
        }

        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            return defaultValue;
        }

        return kind switch
        {
            LogicParamKind.Bool => "false",
            LogicParamKind.Number => "0",
            _ => string.Empty
        };
    }

    private static void EmitWriteMemberLine(
        StringBuilder sb,
        string bindingId,
        string value,
        string? clrType,
        LogicParamKind kind,
        string indent)
    {
        var literal = !string.IsNullOrWhiteSpace(clrType)
            ? FormatCSharpLiteral(value, clrType)
            : FormatCSharpLiteral(value, kind);
        if (!ShouldEmitWriteMember(literal, kind))
        {
            return;
        }

        BindingCodegen.EmitWrite(sb, bindingId, literal, indent);
    }

    private static bool ShouldEmitWriteMember(string literal, LogicParamKind kind) =>
        kind is LogicParamKind.Text or LogicParamKind.Color || literal != "\"\"";

    private static string FormatCSharpLiteral(string value, LogicParamKind kind) => kind switch
    {
        LogicParamKind.Bool => value.Equals("false", StringComparison.OrdinalIgnoreCase) ? "false" : "true",
        LogicParamKind.Number => float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var f)
            ? $"{f.ToString(System.Globalization.CultureInfo.InvariantCulture)}f"
            : "0f",
        _ => $"\"{EscapeString(value)}\""
    };

    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
