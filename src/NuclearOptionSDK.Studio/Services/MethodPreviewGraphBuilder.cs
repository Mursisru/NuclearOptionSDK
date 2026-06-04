using System.Text.RegularExpressions;
using NuclearOptionSDK.Decompiler;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>
/// Builds a readable logic chain from decompiled method preview text (game dump).
/// </summary>
public static class MethodPreviewGraphBuilder
{
    private const int MaxStepsWeakPreview = 12;
    private const int MaxStepsFullPreview = 40;

    private static readonly Regex IfLine = new(
        @"^\s*if\s*\((.+)\)\s*\{?\s*$",
        RegexOptions.Compiled);

    public static LogicGraph Build(GameMemberNode member)
    {
        var preview = member.PreviewText ?? member.Signature;
        var maxSteps = PreviewTextPolicy.IsWeakPreview(member.PreviewText, member.Signature)
            ? MaxStepsWeakPreview
            : MaxStepsFullPreview;
        var lines = preview.Split('\n');
        var nodes = new List<LogicNode>();
        var edges = new List<LogicEdge>();
        var y = 80f;
        var order = 0;

        var entryParams = new Dictionary<string, string>
        {
            ["bindingId"] = member.BindingId,
            ["displayName"] = member.Name
        };
        GameBindingValueSchema.ApplyClrTypeFromDrag(entryParams, member.ClrTypeName);
        var entry = CreateNode("n0", "source", "Member.Bind", ReferenceGraphLayout.NextOrderHint(order++), y, entryParams);
        nodes.Add(entry);
        var lastId = entry.id;
        var stepIndex = 1;

        for (var i = 0; i < lines.Length && stepIndex <= maxSteps; i++)
        {
            var line = lines[i].Trim();
            if (IsSkippableLine(line))
            {
                continue;
            }

            if (IfLine.Match(line) is { Success: true } ifMatch)
            {
                var cond = TrimCondition(ifMatch.Groups[1].Value);
                var gate = CreateNode($"n{stepIndex}", "gate", "Gate.OnlyWhen", ReferenceGraphLayout.NextOrderHint(order++), y, new Dictionary<string, string>
                {
                    ["displayName"] = $"if ({cond})",
                    ["checkTypeId"] = "Compare.GreaterThan",
                    ["threshold"] = "0"
                });
                nodes.Add(gate);
                edges.Add(new LogicEdge { fromNode = lastId, toNode = gate.id, fromPort = "out", toPort = "in" });
                lastId = gate.id;
                stepIndex++;

                var hasInlineBrace = line.Contains('{');
                if (hasInlineBrace || (i + 1 < lines.Length && lines[i + 1].Trim() == "{"))
                {
                    var bodyIndex = i + 1;
                    if (!hasInlineBrace)
                    {
                        bodyIndex++;
                    }

                    var bodyLines = CollectBlockBody(lines, ref bodyIndex);
                    var body = CreateBodyNode($"n{stepIndex}", bodyLines, ReferenceGraphLayout.NextOrderHint(order++), y);
                    nodes.Add(body);
                    edges.Add(new LogicEdge { fromNode = gate.id, toNode = body.id, fromPort = "out", toPort = "in" });
                    lastId = body.id;
                    stepIndex++;
                    i = bodyIndex - 1;
                }

                continue;
            }

            var node = ParseStatement(line, $"n{stepIndex}", ReferenceGraphLayout.NextOrderHint(order++), y, member.Name);
            if (node == null)
            {
                continue;
            }

            nodes.Add(node);
            edges.Add(new LogicEdge { fromNode = lastId, toNode = node.id, fromPort = "out", toPort = "in" });
            lastId = node.id;
            stepIndex++;
        }

        if (nodes.Count == 1)
        {
            var body = CreateNode("n1", "output", "Action.Sequence", ReferenceGraphLayout.NextOrderHint(order++), y, new Dictionary<string, string>
            {
                ["displayName"] = "method body (see preview on the right)"
            });
            nodes.Add(body);
            edges.Add(new LogicEdge { fromNode = lastId, toNode = body.id, fromPort = "out", toPort = "in" });
        }

        return ReferenceGraphLayout.PackHorizontalChain(new LogicGraph
        {
            nodes = nodes.ToArray(),
            edges = edges.ToArray()
        }, y);
    }

    /// <summary>
    /// Collects statements at depth 1 inside a block whose opening "{" was already consumed.
    /// </summary>
    private static List<string> CollectBlockBody(string[] lines, ref int index)
    {
        var body = new List<string>();
        var depth = 1;

        while (index < lines.Length && depth > 0)
        {
            var line = lines[index].Trim();
            index++;

            if (line == "{")
            {
                depth++;
                continue;
            }

            if (line == "}")
            {
                depth--;
                continue;
            }

            if (depth == 1 && !IsSkippableLine(line) && IsStatementLine(line))
            {
                body.Add(line.TrimEnd(';'));
            }
        }

        return body;
    }

    private static LogicNode CreateBodyNode(string id, IReadOnlyList<string> bodyLines, float x, float y)
    {
        if (bodyLines.Count == 0)
        {
            return CreateNode(id, "output", "Action.Sequence", x, y, new Dictionary<string, string>
            {
                ["displayName"] = "if body (see preview)"
            });
        }

        if (bodyLines.Count == 1)
        {
            return CreateNode(id, "output", "Action.Sequence", x, y, new Dictionary<string, string>
            {
                ["displayName"] = bodyLines[0].TrimEnd(';')
            });
        }

        return CreateNode(id, "output", "Action.Sequence", x, y, new Dictionary<string, string>
        {
            ["displayName"] = "then",
            ["steps"] = string.Join("\n", bodyLines)
        });
    }

    private static string TruncateStep(string line) => line.TrimEnd(';');

    private static LogicNode? ParseStatement(string line, string id, float x, float y, string memberName)
    {
        if (line.Contains('=') && !line.Contains("==") && !line.Contains("!=") && !line.Contains("<=") && !line.Contains(">="))
        {
            return CreateNode(id, "output", "Logic.SetState", x, y, new Dictionary<string, string>
            {
                ["displayName"] = line.TrimEnd(';'),
                ["key"] = memberName,
                ["value"] = TrimAssignmentRhs(line)
            });
        }

        if (line.Contains('(') && (line.Contains("new ", StringComparison.Ordinal) || char.IsUpper(line[0]) || line.Contains('.', StringComparison.Ordinal)))
        {
            return CreateNode(id, "output", "Action.Sequence", x, y, new Dictionary<string, string>
            {
                ["displayName"] = line.TrimEnd(';')
            });
        }

        return null;
    }

    private static bool IsSkippableLine(string line) =>
        line.Length == 0
        || line is "{" or "}"
        || line.StartsWith("//", StringComparison.Ordinal)
        || line.StartsWith("private ", StringComparison.Ordinal)
        || line.StartsWith("public ", StringComparison.Ordinal)
        || line.StartsWith("protected ", StringComparison.Ordinal)
        || line.Contains(" RVA: ", StringComparison.Ordinal);

    private static bool IsStatementLine(string line) =>
        line.Contains('=') || line.Contains('(');

    private static LogicNode CreateNode(
        string id,
        string kind,
        string typeId,
        float x,
        float y,
        Dictionary<string, string> parameters)
    {
        return new LogicNode
        {
            id = id,
            kind = kind,
            typeId = typeId,
            x = x,
            y = y,
            parameters = parameters
        };
    }

    private static string TrimCondition(string cond) => cond.Replace('\r', ' ').Trim();

    private static string TrimAssignmentRhs(string line)
    {
        var idx = line.IndexOf('=');
        if (idx < 0)
        {
            return string.Empty;
        }

        return Truncate(line[(idx + 1)..].Trim().TrimEnd(';'), 32);
    }

    private static string Truncate(string text, int max)
    {
        text = text.Replace('\r', ' ').Trim();
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
