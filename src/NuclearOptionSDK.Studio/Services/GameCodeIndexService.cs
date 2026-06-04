using NuclearOptionSDK.Studio.Services.ApiSurface;
using NuclearOptionSDK.Studio.Services.ApiSurface.Context;
using NuclearOptionSDK.Studio.Services.ApiSurface.Filtering;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public enum GameMemberKind
{
    /// <summary>Scalar field/property (int, float, string, bool).</summary>
    Parameter,

    /// <summary>Method or constructor — action with ().</summary>
    Method,

    /// <summary>Reference to a game object type (Player, Aircraft, …).</summary>
    Value
}

public sealed class GameMemberNode
{
    public required GameMemberKind Kind { get; init; }
    public required MemberBehaviorBucket Behavior { get; init; }
    public required string Name { get; init; }
    public required string Signature { get; init; }
    public required string BindingId { get; init; }
    public string? SourceFile { get; init; }
    public int SourceLine { get; init; }
    public string? PreviewText { get; init; }
    public ApiSymbolId SymbolId { get; init; }
    public ApiMemberSource Source { get; init; }
    public ApiSurfaceCategory Category { get; init; }
    public string ClrTypeName { get; init; } = string.Empty;
    public ClrTypeKind ClrKind { get; init; }
    public OwnerContextKind OwnerContext { get; init; }
    public string? Hint { get; init; }
    public string? ContextTitle { get; init; }
    public string? InspectorLine { get; init; }
    public string? CollisionBadge { get; init; }
    public IReadOnlyList<DependencyNode> Writers { get; set; } = Array.Empty<DependencyNode>();
    public IReadOnlyList<DependencyNode> Readers { get; set; } = Array.Empty<DependencyNode>();
    public IReadOnlyList<string> DependencyWarnings { get; set; } = Array.Empty<string>();
}

public sealed class GameTypeNode
{
    public required string FullName { get; init; }
    public required string ShortName { get; init; }
    /// <summary>Scalar data fields/properties.</summary>
    public required IReadOnlyList<GameMemberNode> Parameters { get; init; }
    /// <summary>Methods and constructors ([ACTIONS]).</summary>
    public required IReadOnlyList<GameMemberNode> Methods { get; init; }
    /// <summary>Object references ([REFERENCES]).</summary>
    public required IReadOnlyList<GameMemberNode> Values { get; init; }
    public int PriorityScore { get; init; }
    public string? DisplayTag { get; init; }
    public ApiSurfaceCategory Category { get; init; }
    public OwnerContextKind OwnerContext { get; init; }

    public IEnumerable<GameMemberNode> AllMembers() =>
        Parameters.Concat(Methods).Concat(Values);
}

public sealed class GameCodeLoadOptions
{
    public bool HideSystemNoise { get; init; } = true;
    public bool ShowUnityLifecycle { get; init; }
}

public static class GameCodeIndexService
{
    public static IReadOnlyList<GameTypeNode> LoadFromGame(
        string nuclearOptionRoot,
        string? filter = null,
        GameCodeLoadOptions? options = null)
    {
        ApiSurfaceRules? rules = null;
        if (options != null)
        {
            var baseRules = ApiSurfaceIndex.Rules;
            rules = new ApiSurfaceRules
            {
                HideSystemNoise = options.HideSystemNoise,
                HideUnityLifecycle = !options.ShowUnityLifecycle,
                HideCompilerGenerated = baseRules.HideCompilerGenerated,
                TypePriorityBoost = baseRules.TypePriorityBoost,
                LifecycleMethodNames = baseRules.LifecycleMethodNames
            };
        }

        var types = ApiSurfaceIndex.Build(nuclearOptionRoot, filter, rules);
        var mapped = types
            .Select(BuildTypeNode)
            .Where(t => string.IsNullOrWhiteSpace(filter)
                        || t.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || t.ShortName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        ApplyDependencyRadar(mapped, nuclearOptionRoot);
        return mapped;
    }

    private static GameTypeNode BuildTypeNode(ApiTypeModel model)
    {
        var actions = new List<GameMemberNode>();
        var data = new List<GameMemberNode>();
        var references = new List<GameMemberNode>();

        foreach (var member in model.Members)
        {
            var node = ToMemberNode(member, model.FullName);
            switch (node.Behavior)
            {
                case MemberBehaviorBucket.Action:
                    actions.Add(node);
                    break;
                case MemberBehaviorBucket.Reference:
                    references.Add(node);
                    break;
                default:
                    data.Add(node);
                    break;
            }
        }

        return new GameTypeNode
        {
            FullName = model.FullName,
            ShortName = model.Name,
            PriorityScore = model.PriorityScore,
            DisplayTag = model.DisplayTag,
            Category = model.Category,
            OwnerContext = OwnerContextClassifier.ClassifyType(model.Name),
            Parameters = data,
            Methods = actions,
            Values = references
        };
    }

    private static GameMemberNode ToMemberNode(ApiMemberModel member, string ownerTypeFullName)
    {
        var ctx = MemberContextAnalyzer.Analyze(
            ownerTypeFullName,
            member.Id.Kind,
            member.TechnicalName,
            member.ClrTypeName,
            member.Signature);

        var kind = ctx.Behavior switch
        {
            MemberBehaviorBucket.Action => GameMemberKind.Method,
            MemberBehaviorBucket.Reference => GameMemberKind.Value,
            _ => GameMemberKind.Parameter
        };

        return new GameMemberNode
        {
            Kind = kind,
            Behavior = ctx.Behavior,
            Name = member.TechnicalName,
            Signature = member.Signature,
            BindingId = member.BindingId,
            SymbolId = member.Id,
            Source = member.Source,
            Category = member.Category,
            ClrTypeName = member.ClrTypeName,
            ClrKind = ctx.ValueTypeKind,
            OwnerContext = ctx.OwnerContext,
            Hint = ctx.ContextHint,
            ContextTitle = ctx.FriendlyTitle,
            InspectorLine = ctx.InspectorLine,
            CollisionBadge = ApiSurface.Labeling.SymbolLabelService.CollisionBadge(member),
            SourceFile = null,
            SourceLine = 0,
            PreviewText = null,
            Writers = Array.Empty<DependencyNode>(),
            Readers = Array.Empty<DependencyNode>(),
            DependencyWarnings = Array.Empty<string>()
        };
    }

    private static void ApplyDependencyRadar(IReadOnlyList<GameTypeNode> types, string nuclearOptionRoot)
    {
        var radar = DependencyRadarService.BuildIndex(nuclearOptionRoot);
        if (radar.Count == 0)
        {
            return;
        }

        foreach (var type in types)
        {
            foreach (var member in type.AllMembers())
            {
                if (!radar.TryGetValue(member.BindingId, out var payload))
                {
                    continue;
                }

                member.Writers = payload.writers;
                member.Readers = payload.readers;
                member.DependencyWarnings = payload.warnings;
            }
        }
    }
}
