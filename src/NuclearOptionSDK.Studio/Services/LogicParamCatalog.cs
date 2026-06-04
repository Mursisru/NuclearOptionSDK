using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Facade over <see cref="NoGameParameterCatalog"/> for Inspector and Logic Graph.</summary>
public static class LogicParamCatalog
{
    public static CatalogGroup[] ReadingGroups => NoGameParameterCatalog.ReadingGroups;

    public static CatalogGroup[] TargetGroups => NoGameParameterCatalog.TargetGroups;

    public static string[] CompareModes => NoGameParameterCatalog.FlatCompareModes().ToArray();

    public static string[] OutputChangeModes => NoGameParameterCatalog.FlatOutputActions().ToArray();

    public static string[] BranchChoices = ["whenTrue", "whenFalse"];

    public static CatalogGroup[] MethodGroups => NoGameParameterCatalog.MethodGroups;

    public static string BranchLabel(string branch) => branch switch
    {
        "whenTrue" => "When condition is met",
        "whenFalse" => "When condition is NOT met",
        _ => branch
    };

    public static IReadOnlyList<string> FlatReadings() => NoGameParameterCatalog.FlatReadings();

    public static IReadOnlyList<string> WatchParamChoices() => NoGameParameterCatalog.FlatWatchParams();

    /// <summary>watchParam list: parameter from chain (bindingId for Member.Bind).</summary>
    public static IReadOnlyList<string> WatchParamChoicesForNode(LogicNode node, LogicGraph? graph = null)
    {
        var upstream = ResolveUpstreamWatchParam(node, graph);
        return string.IsNullOrWhiteSpace(upstream)
            ? Array.Empty<string>()
            : new[] { upstream };
    }

    /// <summary>Curated catalog + Game Code index (e.g. isLanded) + upstream from chain.</summary>
    public static IReadOnlyList<string> SearchWatchParamIds(
        string? query,
        LogicNode node,
        LogicGraph? graph,
        int maxResults = 40)
    {
        var q = query?.Trim() ?? string.Empty;
        var upstream = ResolveUpstreamWatchParam(node, graph);
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Consider(string id, int score)
        {
            if (string.IsNullOrWhiteSpace(id) || score < 0)
            {
                return;
            }

            if (!scores.TryGetValue(id, out var prev) || score > prev)
            {
                scores[id] = score;
            }
        }

        if (string.IsNullOrEmpty(q))
        {
            if (!string.IsNullOrWhiteSpace(upstream))
            {
                Consider(upstream, 10_000);
            }

            return scores
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => WatchParamFriendlyTitle(kv.Key, graph), StringComparer.CurrentCultureIgnoreCase)
                .Take(maxResults)
                .Select(kv => kv.Key)
                .ToList();
        }

        foreach (var id in NoGameParameterCatalog.FlatWatchParams())
        {
            var score = Math.Max(
                Math.Max(FuzzySearchService.Score(id, q), FuzzySearchService.Score(FriendlyTitle(id), q)),
                FuzzySearchService.Score(Title(id), q));
            Consider(id, score);
        }

        foreach (var (member, score) in GameCodeIndexCache.SearchWatchableMembers(q, maxResults))
        {
            Consider(member.BindingId, score);
        }

        if (!string.IsNullOrWhiteSpace(upstream))
        {
            var upstreamScore = Math.Max(
                5_000,
                Math.Max(
                    FuzzySearchService.Score(upstream, q),
                    FuzzySearchService.Score(WatchParamFriendlyTitle(upstream, graph), q)));
            Consider(upstream, upstreamScore);
        }

        return scores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => WatchParamFriendlyTitle(kv.Key, graph), StringComparer.CurrentCultureIgnoreCase)
            .Take(maxResults)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>watchParam from upstream source (Read.* / Telemetry.* / bindingId Member.Bind).</summary>
    public static string? ResolveUpstreamWatchParam(LogicNode node, LogicGraph? graph)
    {
        if (graph == null)
        {
            return null;
        }

        var incoming = graph.edges.Where(e => e.toNode == node.id).Select(e => e.fromNode).ToList();
        foreach (var fromId in incoming)
        {
            var from = graph.nodes.FirstOrDefault(n => n.id == fromId);
            if (from == null)
            {
                continue;
            }

            if (from.kind == "source" && !string.IsNullOrWhiteSpace(from.typeId))
            {
                if (from.typeId.StartsWith("Read.", StringComparison.Ordinal)
                    || from.typeId.StartsWith("Telemetry.", StringComparison.Ordinal))
                {
                    return from.typeId;
                }

                if (from.typeId == "Member.Bind"
                    && from.parameters.TryGetValue("bindingId", out var bid)
                    && !string.IsNullOrWhiteSpace(bid))
                {
                    return bid;
                }
            }

            var nested = ResolveUpstreamWatchParam(from, graph);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>Default watch binding for legacy Gate.* checks (from curated catalog GamePath).</summary>
    public static string? ResolveDefaultWatchBindingForCheck(string checkTypeId)
    {
        if (string.IsNullOrWhiteSpace(checkTypeId))
        {
            return null;
        }

        var catalogId = checkTypeId switch
        {
            "Gate.OnlyWhenInFlight" => "Telemetry.Speed",
            "Gate.WhileAirborne" or "Gate.WhileOnGround" => "Telemetry.Altitude",
            "Gate.FuelLow" => "Telemetry.Fuel",
            "Gate.GearUp" or "Gate.GearDown" => "Telemetry.GearDown",
            "Gate.WeaponSelected" => "Weapon.StationCount",
            _ => null
        };

        return catalogId == null ? null : TryWatchBindingFromCatalogId(catalogId);
    }

    private static string? TryWatchBindingFromCatalogId(string catalogId)
    {
        if (!NoGameParameterCatalog.TryGet(catalogId, out var entry) || string.IsNullOrWhiteSpace(entry.GamePath))
        {
            return null;
        }

        return TryGamePathToWatchBinding(entry.GamePath);
    }

    private static string? TryGamePathToWatchBinding(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath)
            || gamePath.StartsWith("derived:", StringComparison.OrdinalIgnoreCase)
            || gamePath.StartsWith("logic:", StringComparison.OrdinalIgnoreCase)
            || gamePath.StartsWith("Gate.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dot = gamePath.IndexOf('.');
        if (dot <= 0 || dot >= gamePath.Length - 1)
        {
            return null;
        }

        var typeName = gamePath[..dot];
        var memberPart = gamePath[(dot + 1)..];
        var paren = memberPart.IndexOf('(');
        if (paren >= 0)
        {
            memberPart = memberPart[..paren];
        }

        if (string.IsNullOrWhiteSpace(memberPart))
        {
            return null;
        }

        return NoGameParameterCatalog.TryResolveReadId(typeName, memberPart);
    }

    /// <summary>Friendly label for watchParam (Read.* catalog or Member.* binding).</summary>
    public static string WatchParamFriendlyTitle(string id, LogicGraph? graph = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        if (TryGetSourceDisplayName(graph, id, out var displayName))
        {
            return displayName;
        }

        if (NoGameParameterCatalog.TryGet(id, out _) || NoGameParameterCatalog.TryGetMethod(id, out _))
        {
            return FriendlyTitle(id);
        }

        if (TryParseMemberBinding(id, out var typeName, out var memberName))
        {
            return NoGameParameterCatalog.FriendlyMemberLabel(typeName, memberName);
        }

        return id;
    }

    /// <summary>Picker label: friendly name + bindingId secondary line.</summary>
    public static string WatchParamDisplayLabel(string id, LogicGraph? graph = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "— not selected —";
        }

        if (NoGameParameterCatalog.TryGet(id, out var entry) && !string.IsNullOrWhiteSpace(entry.GamePath))
        {
            return DisplayLabel(id);
        }

        var friendly = WatchParamFriendlyTitle(id, graph);
        if (IsMemberStyleBinding(id) && !string.Equals(friendly, id, StringComparison.Ordinal))
        {
            return $"{friendly}\n{id}";
        }

        return friendly;
    }

    public static bool IsMemberStyleBinding(string id) =>
        id.StartsWith("Member.", StringComparison.Ordinal)
        || (!id.StartsWith("Read.", StringComparison.Ordinal)
            && !id.StartsWith("Telemetry.", StringComparison.Ordinal)
            && id.Contains('.', StringComparison.Ordinal));

    public static void TryAutoFillWatchParamFromEdge(LogicGraph graph, LogicEdge edge)
    {
        var to = graph.nodes.FirstOrDefault(n => n.id == edge.toNode);
        if (to == null || to.kind is not ("check" or "gate"))
        {
            return;
        }

        if (to.parameters.TryGetValue("watchParam", out var existing) && !string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        var upstream = ResolveUpstreamWatchParam(to, graph);
        if (!string.IsNullOrWhiteSpace(upstream))
        {
            to.parameters["watchParam"] = upstream;
            GameBindingValueSchema.ApplyWatchParamMetadata(to, graph);
            GameBindingValueSchema.ApplyDefaultExpectForKind(to, graph);
        }
    }

    private static bool TryGetSourceDisplayName(LogicGraph? graph, string watchParam, out string displayName)
    {
        displayName = string.Empty;
        if (graph == null)
        {
            return false;
        }

        foreach (var source in graph.nodes.Where(n => n.kind == "source"))
        {
            if (source.parameters.TryGetValue("bindingId", out var bid)
                && string.Equals(bid, watchParam, StringComparison.OrdinalIgnoreCase)
                && source.parameters.TryGetValue("displayName", out var dn)
                && !string.IsNullOrWhiteSpace(dn))
            {
                displayName = dn;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseMemberBinding(string bindingId, out string typeName, out string memberName)
    {
        typeName = string.Empty;
        memberName = string.Empty;
        if (!bindingId.StartsWith("Member.", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = bindingId.Split('.');
        if (parts.Length < 3)
        {
            return false;
        }

        memberName = parts[^1];
        typeName = string.Join('.', parts.Skip(1).Take(parts.Length - 2));
        return true;
    }

    public static IReadOnlyList<string> FlatTargets() => NoGameParameterCatalog.FlatTargets();

    public static IReadOnlyList<string> FlatMethods() => NoGameParameterCatalog.FlatMethods();

    public static string Title(string id) => NoGameParameterCatalog.Title(id);

    public static string FriendlyTitle(string id) => NoGameParameterCatalog.FriendlyTitle(id);

    public static string Description(string id) => NoGameParameterCatalog.Description(id);

    public static string Hint(string id) => NoGameParameterCatalog.Hint(id);

    public static string PaletteCaption(string id) => NoGameParameterCatalog.PaletteCaption(id);

    public static string DisplayLabel(string id)
    {
        if (IsMemberStyleBinding(id) && !NoGameParameterCatalog.TryGet(id, out _))
        {
            return WatchParamDisplayLabel(id);
        }

        return NoGameParameterCatalog.DisplayLabel(id);
    }

    /// <summary>Quick numeric hints for the selected parameter only.</summary>
    public static IReadOnlyList<string> QuickValuesForParam(string? paramId) =>
        NoGameParameterCatalog.QuickValues(paramId);

    public static string ResolveContextParam(LogicNode node) =>
        node.parameters.TryGetValue("watchParam", out var wp) && !string.IsNullOrWhiteSpace(wp) ? wp
        : node.kind == "source" ? node.typeId
        : node.parameters.TryGetValue("sourceParam", out var sp) && !string.IsNullOrWhiteSpace(sp) ? sp
        : node.parameters.TryGetValue("gameTarget", out var gt) && !string.IsNullOrWhiteSpace(gt) ? gt
        : node.typeId;
}

public sealed record CatalogGroup(string Title, string[] Items);
