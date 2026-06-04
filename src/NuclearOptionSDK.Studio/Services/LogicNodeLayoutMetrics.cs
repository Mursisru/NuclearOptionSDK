using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public static class LogicNodeLayoutMetrics
{
    private static readonly DisplayLayerService Display = new();

    public static double EstimateWidth(LogicNode node)
    {
        var lines = CollectLayoutLines(node);
        var fontSize = LogicEdgeRouting.GetSteps(node).Count > 1 ? 10d : 11d;
        return LogicEdgeRouting.EstimateNodeWidth(lines, fontSize);
    }

    public static IEnumerable<string> CollectLayoutLines(LogicNode node)
    {
        var lines = new List<string>();

        if (node.parameters.TryGetValue("displayName", out var dn) && !string.IsNullOrWhiteSpace(dn))
        {
            lines.Add(dn);
        }
        else
        {
            lines.Add(Display.Title(node.typeId));
        }

        foreach (var step in LogicEdgeRouting.GetSteps(node))
        {
            lines.Add(step);
        }

        var summary = LogicNodeParameterSchema.FormatSummary(node, Display);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            lines.Add(summary);
        }

        return lines;
    }
}
