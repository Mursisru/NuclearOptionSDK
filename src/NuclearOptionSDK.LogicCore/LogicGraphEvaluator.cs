using System;
using System.Collections.Generic;
using System.Linq;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.LogicCore;

public sealed class LogicGraphEvaluator
{
    public LogicEvaluationResult Evaluate(LogicProject project, ITelemetryContext telemetry, ILogicNodeState? state = null)
    {
        return EvaluateGraph(project.userGraph, project.mergeMode, telemetry, state);
    }

    public LogicEvaluationResult EvaluateGraph(
        LogicGraph graph,
        string mergeMode,
        ITelemetryContext telemetry,
        ILogicNodeState? state = null)
    {
        state ??= new NullLogicNodeState();
        var nodeMap = graph.nodes.ToDictionary(n => n.id, StringComparer.Ordinal);
        var incoming = BuildIncoming(graph.edges);
        var outgoing = BuildOutgoing(graph.edges);

        var sourceNodes = graph.nodes.Where(n => n.kind == "source").ToList();
        if (sourceNodes.Count == 0)
        {
            return LogicEvaluationResult.Empty;
        }

        var branchResults = new List<bool>();
        var actions = new List<LogicActionResult>();
        var nodeValues = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var source in sourceNodes)
        {
            var sourceValue = ResolveSourceValue(source, telemetry);
            nodeValues[source.id] = true;

            foreach (var nextId in GetTargets(source.id, outgoing))
            {
                EvaluateBranch(nextId, sourceValue, nodeMap, incoming, outgoing, telemetry, state, nodeValues, branchResults, actions);
            }
        }

        var mergeNodes = graph.nodes.Where(n => n.kind == "merge").ToList();
        if (mergeNodes.Count > 0)
        {
            foreach (var merge in mergeNodes)
            {
                var inputs = GetSources(merge.id, incoming);
                var passed = mergeMode == "any"
                    ? inputs.Any(id => nodeValues.TryGetValue(id, out var v) && v)
                    : inputs.All(id => nodeValues.TryGetValue(id, out var v) && v);

                nodeValues[merge.id] = passed;
                if (passed)
                {
                    foreach (var nextId in GetTargets(merge.id, outgoing))
                    {
                        EvaluateOutputChain(nextId, nodeMap, incoming, outgoing, telemetry, state, nodeValues, actions, true);
                    }
                }
            }
        }
        else if (branchResults.Count > 0)
        {
            var passed = mergeMode == "any" ? branchResults.Any(r => r) : branchResults.All(r => r);
            if (!passed)
            {
                actions.Clear();
            }
        }

        return new LogicEvaluationResult(actions.ToArray(), nodeValues);
    }

    private void EvaluateBranch(
        string nodeId,
        double sourceValue,
        Dictionary<string, LogicNode> nodeMap,
        Dictionary<string, List<string>> incoming,
        Dictionary<string, List<string>> outgoing,
        ITelemetryContext telemetry,
        ILogicNodeState state,
        Dictionary<string, bool> nodeValues,
        List<bool> branchResults,
        List<LogicActionResult> actions)
    {
        if (!nodeMap.TryGetValue(nodeId, out var node))
        {
            return;
        }

        switch (node.kind)
        {
            case "check":
            case "gate":
                var passed = EvaluateCheck(node, sourceValue, telemetry, state);
                nodeValues[nodeId] = passed;
                if (!passed)
                {
                    branchResults.Add(false);
                    return;
                }

                var nextTargets = GetTargets(nodeId, outgoing);
                if (nextTargets.Count == 0)
                {
                    branchResults.Add(true);
                    return;
                }

                foreach (var nextId in nextTargets)
                {
                    EvaluateBranch(nextId, sourceValue, nodeMap, incoming, outgoing, telemetry, state, nodeValues, branchResults, actions);
                }
                break;

            case "output":
                if (nodeValues.Values.All(v => v))
                {
                    actions.AddRange(EvaluateOutput(node));
                }
                branchResults.Add(true);
                break;

            case "merge":
                nodeValues[nodeId] = true;
                foreach (var nextId in GetTargets(nodeId, outgoing))
                {
                    EvaluateOutputChain(nextId, nodeMap, incoming, outgoing, telemetry, state, nodeValues, actions, true);
                }
                break;

            default:
                foreach (var nextId in GetTargets(nodeId, outgoing))
                {
                    EvaluateBranch(nextId, sourceValue, nodeMap, incoming, outgoing, telemetry, state, nodeValues, branchResults, actions);
                }
                break;
        }
    }

    private void EvaluateOutputChain(
        string nodeId,
        Dictionary<string, LogicNode> nodeMap,
        Dictionary<string, List<string>> incoming,
        Dictionary<string, List<string>> outgoing,
        ITelemetryContext telemetry,
        ILogicNodeState state,
        Dictionary<string, bool> nodeValues,
        List<LogicActionResult> actions,
        bool parentPassed)
    {
        if (!nodeMap.TryGetValue(nodeId, out var node) || !parentPassed)
        {
            return;
        }

        if (node.kind == "output")
        {
            actions.AddRange(EvaluateOutput(node));
            ProcessPostChain(node, nodeMap, outgoing, telemetry, state, actions);
            return;
        }

        foreach (var nextId in GetTargets(nodeId, outgoing))
        {
            EvaluateOutputChain(nextId, nodeMap, incoming, outgoing, telemetry, state, nodeValues, actions, parentPassed);
        }
    }

    private void ProcessPostChain(
        LogicNode outputNode,
        Dictionary<string, LogicNode> nodeMap,
        Dictionary<string, List<string>> outgoing,
        ITelemetryContext telemetry,
        ILogicNodeState state,
        List<LogicActionResult> actions)
    {
        if (outputNode.typeId == "Action.ThenAfter" &&
            outputNode.parameters.TryGetValue("nextNodeId", out var nextId) &&
            outputNode.parameters.TryGetValue("delaySec", out var delayStr) &&
            double.TryParse(delayStr, out var delaySec))
        {
            if (state.IsPostChainReady(outputNode.id, delaySec))
            {
                if (nodeMap.TryGetValue(nextId, out var nextNode) && nextNode.kind == "output")
                {
                    actions.AddRange(EvaluateOutput(nextNode));
                }
            }
        }
    }

    public static double ResolveSourceValue(LogicNode node, ITelemetryContext telemetry)
    {
        return node.typeId switch
        {
            "Telemetry.AoA" => telemetry.TryGetFloat("Telemetry.AoA", out var aoa) ? aoa : 0,
            "Telemetry.Speed" => telemetry.TryGetFloat("Telemetry.Speed", out var speed) ? speed : 0,
            "Telemetry.Altitude" => telemetry.TryGetFloat("Telemetry.Altitude", out var alt) ? alt : 0,
            "Telemetry.G" => telemetry.TryGetFloat("Telemetry.G", out var g) ? g : 0,
            _ when node.parameters.TryGetValue("bindingId", out var bindId) &&
                   telemetry.TryGetFloat(bindId, out var val) => val,
            _ => 0
        };
    }

    public static bool EvaluateCheck(LogicNode node, double sourceValue, ITelemetryContext telemetry, ILogicNodeState state)
    {
        var threshold = GetParamDouble(node, "threshold", 0);
        return node.typeId switch
        {
            "Compare.GreaterThan" => sourceValue > threshold,
            "Compare.GreaterOrEqual" => sourceValue >= threshold,
            "Compare.LessThan" => sourceValue < threshold,
            "Compare.LessOrEqual" => sourceValue <= threshold,
            "Compare.Equals" => Math.Abs(sourceValue - threshold) < 1e-6,
            "Compare.NotEquals" => Math.Abs(sourceValue - threshold) >= 1e-6,
            "Compare.Approximately" => Math.Abs(sourceValue - threshold) <= GetParamDouble(node, "epsilon", 0.01),
            "Compare.IsTrue" => Math.Abs(sourceValue) > 1e-6,
            "Compare.IsFalse" => Math.Abs(sourceValue) < 1e-6,
            "Compare.InRange" => sourceValue >= GetParamDouble(node, "min", 0)
                && sourceValue <= GetParamDouble(node, "max", 100),
            "Compare.OutsideRange" => sourceValue < GetParamDouble(node, "min", 0)
                || sourceValue > GetParamDouble(node, "max", 100),
            "Compare.Changed" => state.EvaluateChanged(node.id, sourceValue),
            "Compare.CrossedAbove" => state.EvaluateCrossedAbove(node.id, sourceValue, threshold),
            "Compare.CrossedBelow" => EvaluateCrossedBelow(state, node.id, sourceValue, threshold),
            "Compare.Debounce" => sourceValue > threshold
                && state.EvaluateDelayBeforeShow(node.id, GetParamDouble(node, "holdSeconds", 0.5)),
            "Gate.OnlyWhenInFlight" => telemetry.TryGetBool("Gate.InFlight", out var inFlight) && inFlight,
            "Gate.OnlyWhen" => EvaluateGateOnlyWhen(node, sourceValue, telemetry, state),
            "Gate.HideWhen" => !EvaluateGateOnlyWhen(node, sourceValue, telemetry, state),
            "Gate.DelayBeforeShow" => state.EvaluateDelayBeforeShow(node.id, GetParamDouble(node, "holdSeconds", GetParamDouble(node, "seconds", 1))),
            "Gate.BlinkWhileTrue" => sourceValue > threshold && state.EvaluateBlink(node.id, GetParamDouble(node, "intervalSec", 0.5)),
            "Gate.Cooldown" => state.EvaluateCooldown(node.id, GetParamDouble(node, "holdSeconds", GetParamDouble(node, "seconds", 2))),
            "Gate.FuelLow" => telemetry.TryGetFloat("Telemetry.Fuel", out var fuel) && fuel < threshold,
            _ => sourceValue > threshold
        };
    }

    private static bool EvaluateGateOnlyWhen(LogicNode node, double sourceValue, ITelemetryContext telemetry, ILogicNodeState state)
    {
        if (node.parameters.TryGetValue("checkTypeId", out var checkType))
        {
            var synthetic = new LogicNode
            {
                id = node.id + "_inner",
                typeId = checkType,
                parameters = node.parameters
            };
            return EvaluateCheck(synthetic, sourceValue, telemetry, state);
        }

        return sourceValue > GetParamDouble(node, "threshold", 0);
    }

    private static bool EvaluateCrossedBelow(ILogicNodeState state, string nodeId, double value, double threshold)
    {
        if (state is LogicStateStore store)
        {
            return store.EvaluateCrossedBelow(nodeId, value, threshold);
        }

        return value < threshold;
    }

    public static IEnumerable<LogicActionResult> EvaluateOutput(LogicNode node)
    {
        foreach (var memberWrite in EvaluateMemberWrite(node))
        {
            yield return memberWrite;
        }

        if (HasMultiOutputChanges(node))
        {
            foreach (var action in EvaluateMultiOutputChanges(node))
            {
                yield return action;
            }

            yield break;
        }

        switch (node.typeId)
        {
            case "Action.SetOverlayColor":
                yield return new LogicActionResult
                {
                    typeId = node.typeId,
                    labelId = GetParam(node, "labelId"),
                    colorHtml = GetParam(node, "colorHtml", "#FF0000")
                };
                break;

            case "Action.SetOverlayVisible":
                yield return new LogicActionResult
                {
                    typeId = node.typeId,
                    labelId = GetParam(node, "labelId"),
                    visible = GetParamBool(node, "visible", true)
                };
                break;

            case "Action.SetHudColor":
                yield return new LogicActionResult
                {
                    typeId = node.typeId,
                    hudInstanceId = GetParamInt(node, "instanceId"),
                    colorHtml = GetParam(node, "colorHtml", "#FFFFFF")
                };
                break;

            case "Action.SetHudText":
                yield return new LogicActionResult
                {
                    typeId = node.typeId,
                    hudInstanceId = GetParamInt(node, "instanceId"),
                    text = GetParam(node, "text", string.Empty)
                };
                break;

            case "Action.SetHudActive":
                yield return new LogicActionResult
                {
                    typeId = node.typeId,
                    hudInstanceId = GetParamInt(node, "instanceId"),
                    visible = GetParamBool(node, "active", true)
                };
                break;

            case "Action.PlaySound":
                yield return new LogicActionResult
                {
                    typeId = node.typeId,
                    text = GetParam(node, "clipName")
                };
                break;
        }
    }

    private static bool HasMultiOutputChanges(LogicNode node) =>
        node.parameters.Keys.Any(k => k.StartsWith("out.", StringComparison.Ordinal) && k.EndsWith(".on", StringComparison.Ordinal));

    private static IEnumerable<LogicActionResult> EvaluateMultiOutputChanges(LogicNode node)
    {
        var target = GetParam(node, "targetId");
        if (string.IsNullOrWhiteSpace(target))
        {
            target = GetParam(node, "labelId", "aoa-label");
        }

        if (IsOutOn(node, "color"))
        {
            yield return new LogicActionResult
            {
                typeId = "Action.SetOverlayColor",
                labelId = target,
                colorHtml = GetOutVal(node, "color", "#FF4400")
            };
        }

        if (IsOutOn(node, "visible"))
        {
            yield return new LogicActionResult
            {
                typeId = "Action.SetOverlayVisible",
                labelId = target,
                visible = GetOutVal(node, "visible", "true") != "false"
            };
        }

        if (IsOutOn(node, "text"))
        {
            yield return new LogicActionResult
            {
                typeId = "Action.SetHudText",
                hudInstanceId = 0,
                text = GetOutVal(node, "text", string.Empty)
            };
        }

        if (IsOutOn(node, "sound"))
        {
            yield return new LogicActionResult
            {
                typeId = "Action.PlaySound",
                text = GetOutVal(node, "sound", string.Empty)
            };
        }

        foreach (var key in node.parameters.Keys)
        {
            if (!key.StartsWith("out.", StringComparison.Ordinal) || !key.EndsWith(".on", StringComparison.Ordinal))
            {
                continue;
            }

            var id = key.Substring("out.".Length, key.Length - "out.".Length - ".on".Length);
            if (!IsOutOn(node, id) || IsBuiltInOutput(id))
            {
                continue;
            }

            var val = GetOutVal(node, id, string.Empty);
            if (id.StartsWith("Member.", StringComparison.Ordinal))
            {
                yield return new LogicActionResult
                {
                    typeId = "Action.SetMemberBind",
                    labelId = id,
                    text = val,
                    visible = val is "false" ? false : val is "true" ? true : null
                };
                continue;
            }

            yield return new LogicActionResult
            {
                typeId = "Action.CatalogWrite",
                labelId = target,
                text = id,
                colorHtml = val
            };
        }
    }

    private static IEnumerable<LogicActionResult> EvaluateMemberWrite(LogicNode node)
    {
        if (!node.parameters.TryGetValue("memberWriteOn", out var on) || on != "true")
        {
            yield break;
        }

        if (!node.parameters.TryGetValue("memberWriteBindingId", out var bindingId)
            || string.IsNullOrWhiteSpace(bindingId))
        {
            yield break;
        }

        var value = node.parameters.TryGetValue("memberWriteValue", out var v) && !string.IsNullOrWhiteSpace(v)
            ? v
            : "true";

        yield return new LogicActionResult
        {
            typeId = "Action.SetMemberBind",
            labelId = bindingId,
            text = value,
            visible = value is "false" ? false : value is "true" ? true : null
        };
    }

    private static bool IsBuiltInOutput(string id) => id is
        "color" or "text" or "format" or "visible" or "fontSize" or "sound";

    private static bool IsOutOn(LogicNode node, string id) =>
        node.parameters.TryGetValue($"out.{id}.on", out var v) && v == "true";

    private static string GetOutVal(LogicNode node, string id, string fallback) =>
        node.parameters.TryGetValue($"out.{id}.val", out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

    private static double GetParamDouble(LogicNode node, string key, double fallback)
    {
        return node.parameters.TryGetValue(key, out var s) && double.TryParse(s, out var v) ? v : fallback;
    }

    private static string GetParam(LogicNode node, string key, string fallback = "")
    {
        return node.parameters.TryGetValue(key, out var v) ? v : fallback;
    }

    private static bool GetParamBool(LogicNode node, string key, bool fallback)
    {
        return node.parameters.TryGetValue(key, out var s) && bool.TryParse(s, out var v) ? v : fallback;
    }

    private static int GetParamInt(LogicNode node, string key)
    {
        return node.parameters.TryGetValue(key, out var s) && int.TryParse(s, out var v) ? v : 0;
    }

    private static Dictionary<string, List<string>> BuildIncoming(LogicEdge[] edges)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!map.TryGetValue(edge.toNode, out var list))
            {
                list = new List<string>();
                map[edge.toNode] = list;
            }

            list.Add(edge.fromNode);
        }

        return map;
    }

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

    private static List<string> GetTargets(string nodeId, Dictionary<string, List<string>> outgoing)
    {
        return outgoing.TryGetValue(nodeId, out var list) ? list : new List<string>();
    }

    private static List<string> GetSources(string nodeId, Dictionary<string, List<string>> incoming)
    {
        return incoming.TryGetValue(nodeId, out var list) ? list : new List<string>();
    }
}

public sealed class LogicEvaluationResult
{
    public static LogicEvaluationResult Empty { get; } = new(Array.Empty<LogicActionResult>(), new Dictionary<string, bool>());

    public LogicEvaluationResult(LogicActionResult[] actions, Dictionary<string, bool> nodeStates)
    {
        Actions = actions;
        NodeStates = nodeStates;
    }

    public LogicActionResult[] Actions { get; }
    public Dictionary<string, bool> NodeStates { get; }
}
