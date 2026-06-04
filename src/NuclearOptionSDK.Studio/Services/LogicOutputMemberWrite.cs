using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Output block: write a value to the game parameter from the Source chain (Member.Bind / Write.*).</summary>
public static class LogicOutputMemberWrite
{
    public const string OnKey = "memberWriteOn";
    public const string BindingKey = "memberWriteBindingId";
    public const string ValueKey = "memberWriteValue";

    public static bool IsEnabled(LogicNode node) =>
        node.parameters.TryGetValue(OnKey, out var v) && v == "true";

    public static string? GetBindingId(LogicNode node) =>
        node.parameters.TryGetValue(BindingKey, out var id) && !string.IsNullOrWhiteSpace(id) ? id : null;

    public static string GetValue(LogicNode node, string fallback = "true") =>
        node.parameters.TryGetValue(ValueKey, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

    public static void TryAutoFillFromEdge(LogicGraph graph, LogicEdge edge)
    {
        var to = graph.nodes.FirstOrDefault(n => n.id == edge.toNode);
        if (to == null || to.kind != "output")
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(GetBindingId(to)))
        {
            return;
        }

        var upstream = LogicParamCatalog.ResolveUpstreamWatchParam(to, graph);
        if (string.IsNullOrWhiteSpace(upstream) || !CanWriteBinding(upstream))
        {
            return;
        }

        to.parameters[BindingKey] = upstream;
        var clr = GameCodeIndexCache.TryGetClrType(upstream)
                  ?? (NoGameParameterCatalog.TryGet(upstream, out var e) ? e.ValueType : null);
        if (!string.IsNullOrWhiteSpace(clr))
        {
            to.parameters[GameBindingValueSchema.ClrTypeParameterKey] = clr;
        }

        if (!to.parameters.ContainsKey(ValueKey))
        {
            var kind = GameBindingValueSchema.ClassifyClrType(clr);
            to.parameters[ValueKey] = GameBindingValueSchema.DefaultExpectValue(to, kind);
        }
    }

    public static bool CanWriteBinding(string bindingId) =>
        bindingId.StartsWith("Member.", StringComparison.Ordinal)
        || bindingId.StartsWith("Write.", StringComparison.Ordinal)
        || (NoGameParameterCatalog.TryGet(bindingId, out var e)
            && e.Direction is NoGameParameterCatalog.Direction.Write or NoGameParameterCatalog.Direction.Both);

    public static string FriendlyBindingLabel(string bindingId, LogicGraph? graph)
    {
        if (string.IsNullOrWhiteSpace(bindingId))
        {
            return "— connect Source first —";
        }

        if (LogicParamCatalog.IsMemberStyleBinding(bindingId))
        {
            return LogicParamCatalog.WatchParamFriendlyTitle(bindingId, graph);
        }

        return NoGameParameterCatalog.TryGet(bindingId, out _)
            ? LogicParamCatalog.FriendlyTitle(bindingId)
            : bindingId;
    }
}
