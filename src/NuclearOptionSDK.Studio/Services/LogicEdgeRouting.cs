using Avalonia;
using Avalonia.Media;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public static class LogicEdgeRouting
{
    public const double NodeMinWidth = 168;
    public const double NodeMaxWidth = 520;
    /// <summary>Default width for palette nodes and layout spacing fallback.</summary>
    public const double NodeWidth = NodeMinWidth;
    public const double NodeHeight = 58;
    public const double HeaderHeight = 18;
    public const double PortOutset = 6;
    public const double BodyHorizontalInset = 10;
    public const double BodyVerticalInset = 10;
    public const double StackedStepLineHeight = 14;
    public const double StackedStepSeparator = 5;
    public const double StackedBodyPadding = 10;
    public const double StackedBodyMargin = 8;
    /// <summary>~half a glyph at 10–11px — extra inset from node border to text.</summary>
    public const double TextHalfLetterInset = 3;
    private const double BorderThicknessReserve = 2;

    /// <summary>Uniform text inset from node border on all four sides.</summary>
    public static double TextContentInset => BodyHorizontalInset + TextHalfLetterInset;

    public static double TextHorizontalInset => TextContentInset;

    public static double TextVerticalInset => BodyVerticalInset + TextHalfLetterInset;

    public static double ClampNodeWidth(double width) =>
        Math.Clamp(Math.Ceiling(width), NodeMinWidth, NodeMaxWidth);

    public static double BodyContentWidth(double nodeWidth) =>
        nodeWidth - 2 * (TextHorizontalInset + BorderThicknessReserve);

    public static double BodyContentWidth() => BodyContentWidth(NodeMinWidth);

    public static double EstimateNodeWidth(IEnumerable<string> lines, double fontSize = 10.5)
    {
        var maxLine = 0d;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            maxLine = Math.Max(maxLine, line.Length * fontSize * 0.58);
        }

        return ClampNodeWidth(maxLine + 2 * (TextHorizontalInset + BorderThicknessReserve) + 10);
    }

    public static IReadOnlyList<string> GetSteps(LogicNode node)
    {
        if (!node.parameters.TryGetValue("steps", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static bool HasStackedSteps(LogicNode node) => GetSteps(node).Count > 1;

    public static double EstimateWrappedLines(string text, double contentWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1;
        }

        var charsPerLine = Math.Max(12, contentWidth / 6.2);
        var total = 0d;
        foreach (var part in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            total += Math.Max(1, Math.Ceiling(part.Length / charsPerLine));
        }

        return Math.Max(1, total);
    }

    public static double EstimateWrappedLines(string text) =>
        EstimateWrappedLines(text, BodyContentWidth(NodeMaxWidth));

    public static double GetStackedBodyHeight(IReadOnlyList<string> steps, double nodeWidth)
    {
        if (steps.Count == 0)
        {
            return 0;
        }

        var contentWidth = BodyContentWidth(nodeWidth);
        var height = TextVerticalInset * 2;
        for (var i = 0; i < steps.Count; i++)
        {
            if (i > 0)
            {
                height += 7;
            }

            height += EstimateWrappedLines(steps[i], contentWidth) * StackedStepLineHeight;
        }

        return height;
    }

    public static double GetNodeHeight(LogicNode node, double? nodeWidth = null)
    {
        var width = nodeWidth ?? EstimateNodeWidth(GetDisplayLines(node));
        var steps = GetSteps(node);
        if (steps.Count <= 1)
        {
            var text = GetDisplayText(node);
            var bodyHeight = EstimateWrappedLines(text, BodyContentWidth(width)) * StackedStepLineHeight
                + TextVerticalInset * 2;
            return Math.Max(NodeHeight, HeaderHeight + bodyHeight);
        }

        return HeaderHeight + GetStackedBodyHeight(steps, width);
    }

    public static IEnumerable<string> GetDisplayLines(LogicNode node)
    {
        var steps = GetSteps(node);
        if (steps.Count > 1)
        {
            return steps;
        }

        return new[] { GetDisplayText(node) };
    }

    public static string GetDisplayText(LogicNode node) =>
        node.parameters.TryGetValue("displayName", out var dn) && !string.IsNullOrWhiteSpace(dn)
            ? dn
            : node.typeId;

    public static Point PortPoint(LogicNode node, string port, double? width = null, double? height = null) =>
        PortPoint(node.x, node.y, port, height ?? GetNodeHeight(node, width), width ?? EstimateNodeWidth(GetDisplayLines(node)));

    public static bool PortsLocked(LogicEdge edge) =>
        edge.parameters.TryGetValue("lockPorts", out var v) && v == "true";

    public static (string fromPort, string toPort) ResolvePorts(
        LogicEdge edge,
        LogicNode from,
        LogicNode to,
        double? fromWidth = null,
        double? toWidth = null,
        double? fromHeight = null,
        double? toHeight = null)
    {
        if (PortsLocked(edge))
        {
            return (edge.fromPort, edge.toPort);
        }

        var auto = BestPorts(from, to, fromHeight, toHeight, fromWidth, toWidth);
        edge.fromPort = auto.fromPort;
        edge.toPort = auto.toPort;
        return auto;
    }

    public static (string fromPort, string toPort) BestPorts(
        LogicNode from,
        LogicNode to,
        double? fromHeight = null,
        double? toHeight = null,
        double? fromWidth = null,
        double? toWidth = null)
    {
        var fromW = fromWidth ?? EstimateNodeWidth(GetDisplayLines(from));
        var toW = toWidth ?? EstimateNodeWidth(GetDisplayLines(to));
        var fromH = fromHeight ?? GetNodeHeight(from, fromW);
        var toH = toHeight ?? GetNodeHeight(to, toW);
        var fx = from.x + fromW / 2;
        var fy = from.y + fromH / 2;
        var tx = to.x + toW / 2;
        var ty = to.y + toH / 2;
        var dx = tx - fx;
        var dy = ty - fy;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0 ? ("out", "in") : ("in", "out");
        }

        return dy >= 0 ? ("bottom", "top") : ("top", "bottom");
    }

    public static Point PortPoint(double nodeX, double nodeY, string port, double height, double width) =>
        port.ToLowerInvariant() switch
        {
            "top" => new Point(nodeX + width / 2, nodeY),
            "bottom" => new Point(nodeX + width / 2, nodeY + height),
            "left" or "in" => new Point(nodeX, nodeY + height / 2),
            _ => new Point(nodeX + width, nodeY + height / 2)
        };

    public static Geometry BuildGeometry(Point from, Point to, string fromPort, string toPort)
    {
        if (IsHorizontalPort(fromPort) && IsHorizontalPort(toPort))
        {
            var dx = Math.Max(40, Math.Abs(to.X - from.X) * 0.45);
            var c1 = new Point(from.X + (from.X <= to.X ? dx : -dx), from.Y);
            var c2 = new Point(to.X + (from.X <= to.X ? -dx : dx), to.Y);
            return new PathGeometry
            {
                Figures = new PathFigures
                {
                    new PathFigure
                    {
                        StartPoint = from,
                        IsClosed = false,
                        Segments = new PathSegments
                        {
                            new BezierSegment { Point1 = c1, Point2 = c2, Point3 = to }
                        }
                    }
                }
            };
        }

        var elbow = fromPort is "bottom"
            ? new Point(from.X, to.Y)
            : fromPort is "top"
                ? new Point(from.X, to.Y)
                : toPort is "top" or "bottom"
                    ? new Point(to.X, from.Y)
                    : new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);

        return new PathGeometry
        {
            Figures = new PathFigures
            {
                new PathFigure
                {
                    StartPoint = from,
                    IsClosed = false,
                    Segments = new PathSegments
                    {
                        new LineSegment { Point = elbow },
                        new LineSegment { Point = to }
                    }
                }
            }
        };
    }

    private static bool IsHorizontalPort(string port) =>
        port is "out" or "right" or "in" or "left";
}
