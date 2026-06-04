using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public static class ReferenceGraphLayout
{
    public const double MinNodeGap = 40;
    private const float StartX = 40f;
    private const float StartY = 60f;
    private const float SameColumnEpsilon = 24f;
    private const float RowGap = 40f;
    private const float OrderHintStep = 1000f;
    private const float SameRowEpsilon = 32f;

    public static LogicGraph NormalizeForDisplay(LogicGraph graph)
    {
        if (graph.nodes.Length == 0)
        {
            return graph;
        }

        if (IsHorizontalChain(graph))
        {
            return PackHorizontalChain(graph, graph.nodes[0].y);
        }

        var nodes = graph.nodes.Select(CloneNode).ToArray();
        var sorted = nodes.OrderBy(n => n.x).ThenBy(n => n.y).ToList();
        var columns = new List<List<LogicNode>>();
        var currentColumn = new List<LogicNode> { sorted[0] };
        var columnAnchorX = sorted[0].x;

        for (var i = 1; i < sorted.Count; i++)
        {
            var node = sorted[i];
            if (Math.Abs(node.x - columnAnchorX) <= SameColumnEpsilon)
            {
                currentColumn.Add(node);
            }
            else
            {
                columns.Add(currentColumn);
                currentColumn = new List<LogicNode> { node };
                columnAnchorX = node.x;
            }
        }

        columns.Add(currentColumn);

        var xCursor = StartX;
        for (var col = 0; col < columns.Count; col++)
        {
            var column = columns[col].OrderBy(n => n.y).ToList();
            var columnWidth = column.Max(LogicNodeLayoutMetrics.EstimateWidth);
            var rowY = StartY;

            for (var row = 0; row < column.Count; row++)
            {
                column[row].x = xCursor;
                column[row].y = rowY;
                rowY += (float)(LogicEdgeRouting.GetNodeHeight(column[row], columnWidth) + RowGap);
            }

            xCursor += (float)(columnWidth + MinNodeGap);
        }

        return new LogicGraph
        {
            nodes = nodes,
            edges = graph.edges.Select(CloneEdge).ToArray()
        };
    }

    public static LogicGraph PackHorizontalChain(LogicGraph graph, float y = 80f)
    {
        if (graph.nodes.Length == 0)
        {
            return graph;
        }

        var nodes = graph.nodes.Select(CloneNode).ToArray();
        var ordered = OrderChainNodes(nodes, graph.edges);
        var x = StartX;

        foreach (var node in ordered)
        {
            var width = LogicNodeLayoutMetrics.EstimateWidth(node);
            node.x = x;
            node.y = y;
            x += (float)(width + MinNodeGap);
        }

        return new LogicGraph
        {
            nodes = nodes,
            edges = graph.edges.Select(CloneEdge).ToArray()
        };
    }

    public static void ResolveHorizontalOverlaps(IList<LogicNode> nodes)
    {
        if (nodes.Count <= 1)
        {
            return;
        }

        var rows = nodes
            .GroupBy(n => Math.Round(n.y / SameRowEpsilon) * SameRowEpsilon)
            .OrderBy(g => g.Key);

        foreach (var row in rows)
        {
            var sorted = row.OrderBy(n => n.x).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                var prev = sorted[i - 1];
                var curr = sorted[i];
                var minX = prev.x + (float)(LogicNodeLayoutMetrics.EstimateWidth(prev) + MinNodeGap);
                if (curr.x < minX)
                {
                    curr.x = minX;
                }
            }
        }
    }

    public static bool IsHorizontalChain(LogicGraph graph)
    {
        if (graph.nodes.Length <= 1)
        {
            return true;
        }

        var ys = graph.nodes.Select(n => n.y).ToList();
        return ys.Max() - ys.Min() <= SameRowEpsilon;
    }

    public static float NextOrderHint(int order) => order * OrderHintStep;

    private static List<LogicNode> OrderChainNodes(IReadOnlyList<LogicNode> nodes, IReadOnlyList<LogicEdge> edges)
    {
        if (nodes.Count <= 1 || edges.Count == 0)
        {
            return nodes.OrderBy(n => n.x).ThenBy(n => n.y).ToList();
        }

        var incoming = edges.GroupBy(e => e.toNode).ToDictionary(g => g.Key, g => g.Count());
        var start = nodes.FirstOrDefault(n => !incoming.ContainsKey(n.id))
            ?? nodes.OrderBy(n => n.x).First();

        var byId = nodes.ToDictionary(n => n.id);
        var outgoing = edges
            .GroupBy(e => e.fromNode)
            .ToDictionary(g => g.Key, g => g.Select(e => e.toNode).ToList());

        var ordered = new List<LogicNode>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = start;

        while (current != null && visited.Add(current.id))
        {
            ordered.Add(current);
            if (!outgoing.TryGetValue(current.id, out var nextIds))
            {
                break;
            }

            current = nextIds
                .Select(id => byId.GetValueOrDefault(id))
                .FirstOrDefault(n => n != null && !visited.Contains(n.id));
        }

        foreach (var node in nodes.OrderBy(n => n.x).ThenBy(n => n.y))
        {
            if (!visited.Contains(node.id))
            {
                ordered.Add(node);
            }
        }

        return ordered;
    }

    private static LogicNode CloneNode(LogicNode node) => new()
    {
        id = node.id,
        kind = node.kind,
        typeId = node.typeId,
        x = node.x,
        y = node.y,
        parameters = new Dictionary<string, string>(node.parameters)
    };

    private static LogicEdge CloneEdge(LogicEdge edge) => new()
    {
        fromNode = edge.fromNode,
        toNode = edge.toNode,
        fromPort = edge.fromPort,
        toPort = edge.toPort,
        parameters = new Dictionary<string, string>(edge.parameters)
    };
}
