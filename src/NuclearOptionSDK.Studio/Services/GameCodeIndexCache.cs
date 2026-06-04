using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Last loaded Game Code index for watchParam search and clrType lookup.</summary>
public static class GameCodeIndexCache
{
    private static IReadOnlyList<GameTypeNode> _types = Array.Empty<GameTypeNode>();
    private static Dictionary<string, GameMemberNode> _byBinding =
        new(StringComparer.OrdinalIgnoreCase);

    public static bool IsLoaded => _types.Count > 0;

    public static void SetIndex(IReadOnlyList<GameTypeNode> types)
    {
        _types = types;
        var map = new Dictionary<string, GameMemberNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in types)
        {
            foreach (var member in type.AllMembers())
            {
                if (!string.IsNullOrWhiteSpace(member.BindingId))
                {
                    map[member.BindingId] = member;
                }
            }
        }

        _byBinding = map;
    }

    public static void Clear() => SetIndex(Array.Empty<GameTypeNode>());

    public static bool TryGetMember(string bindingId, out GameMemberNode member) =>
        _byBinding.TryGetValue(bindingId, out member!);

    public static GameTypeNode? FindType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        foreach (var type in _types)
        {
            if (type.ShortName.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                || type.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return null;
    }

    public static string? TryGetClrType(string bindingId) =>
        TryGetMember(bindingId, out var m) && !string.IsNullOrWhiteSpace(m.ClrTypeName)
            ? m.ClrTypeName
            : null;

    public static DependencyRadarPayload? TryGetDependencyRadar(string bindingId)
    {
        if (!TryGetMember(bindingId, out var member))
        {
            return null;
        }

        return new DependencyRadarPayload
        {
            bindingId = bindingId,
            readers = member.Readers.ToArray(),
            writers = member.Writers.ToArray(),
            warnings = member.DependencyWarnings.ToArray()
        };
    }

  /// <summary>Data fields and bool-return methods suitable for check nodes.</summary>
    public static IEnumerable<(GameMemberNode Member, int Score)> SearchWatchableMembers(string query, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query) || _types.Count == 0)
        {
            yield break;
        }

        var hits = new List<(GameMemberNode Member, int Score)>();
        foreach (var type in _types)
        {
            foreach (var member in type.AllMembers())
            {
                if (!IsWatchableMember(member))
                {
                    continue;
                }

                var score = ScoreMember(member, type.FullName, query);
                if (score >= 0)
                {
                    hits.Add((member, score));
                }
            }
        }

        foreach (var hit in hits
                     .OrderByDescending(h => h.Score)
                     .ThenBy(h => h.Member.Name, StringComparer.OrdinalIgnoreCase)
                     .Take(maxResults))
        {
            yield return hit;
        }
    }

    private static bool IsWatchableMember(GameMemberNode member) =>
        member.Kind == GameMemberKind.Parameter
        || (member.Kind == GameMemberKind.Method && GameBindingValueSchema.IsBoolClrType(member.ClrTypeName));

    private static int ScoreMember(GameMemberNode member, string typeFullName, string query)
    {
        var label = member.ContextTitle ?? member.Name;
        return Math.Max(
            Math.Max(
                FuzzySearchService.Score(member.Name, query),
                FuzzySearchService.Score(label, query)),
            Math.Max(
                FuzzySearchService.Score(member.BindingId, query),
                FuzzySearchService.Score(typeFullName, query)));
    }
}
