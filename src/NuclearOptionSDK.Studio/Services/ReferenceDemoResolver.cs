using Newtonsoft.Json;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>
/// Maps a palette type or game binding to a full reference graph demo (not a single node).
/// </summary>
public static class ReferenceDemoResolver
{
    private static readonly Dictionary<string, string> ExactTypeMap = new(StringComparer.Ordinal)
    {
        ["Reference.AoADisplay"] = "aoa-display",
        ["Reference.SpeedAltG"] = "speed-alt-g",
        ["Reference.FuelStatus"] = "fuel-status",
        ["Reference.WeaponStatus"] = "weapon-status"
    };

    private static readonly (string token, string refId)[] TokenMap =
    {
        ("AoA", "aoa-display"),
        ("aoa", "aoa-display"),
        ("stall", "aoa-display"),
        ("Fuel", "fuel-status"),
        ("fuel", "fuel-status"),
        ("Weapon", "weapon-status"),
        ("weapon", "weapon-status"),
        ("Missile", "weapon-status"),
        ("Speed", "speed-alt-g"),
        ("Altitude", "speed-alt-g"),
        ("Aircraft", "speed-alt-g"),
        ("aircraft", "speed-alt-g"),
        ("Telemetry.G", "speed-alt-g"),
        ("PlaySound", "aoa-display"),
        ("Audio.", "aoa-display")
    };

    public static ReferenceGraphPayload? ResolveFromPalette(
        string typeId,
        IReadOnlyList<ReferenceGraphPayload>? references = null)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            return null;
        }

        references ??= LogicProjectStore.LoadAllReferences();

        if (ExactTypeMap.TryGetValue(typeId, out var exactId))
        {
            return Pick(references, exactId);
        }

        var byNode = FindBestByNodeTypeId(typeId, references);
        if (byNode != null)
        {
            return Clone(byNode);
        }

        foreach (var (token, refId) in TokenMap)
        {
            if (typeId.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return Pick(references, refId);
            }
        }

        return Pick(references, "aoa-display");
    }

    public static ReferenceGraphPayload? ResolveFromBinding(
        string bindingId,
        IReadOnlyList<ReferenceGraphPayload>? references = null)
    {
        if (string.IsNullOrWhiteSpace(bindingId))
        {
            return null;
        }

        references ??= LogicProjectStore.LoadAllReferences();

        foreach (var payload in references)
        {
            if (payload.graph.nodes.Any(NodeMatchesBinding(bindingId)))
            {
                return Clone(payload);
            }
        }

        foreach (var (token, refId) in TokenMap)
        {
            if (bindingId.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return Pick(references, refId);
            }
        }

        if (bindingId.Contains("velocity", StringComparison.OrdinalIgnoreCase)
            || bindingId.Contains("AoA", StringComparison.OrdinalIgnoreCase))
        {
            return Pick(references, "aoa-display");
        }

        return ResolveFromPalette("Member.Bind", references);
    }

    private static ReferenceGraphPayload? FindBestByNodeTypeId(
        string typeId,
        IReadOnlyList<ReferenceGraphPayload> references)
    {
        ReferenceGraphPayload? best = null;
        var bestScore = 0;

        foreach (var payload in references)
        {
            var score = payload.graph.nodes.Count(n => string.Equals(n.typeId, typeId, StringComparison.Ordinal));
            if (score > bestScore)
            {
                bestScore = score;
                best = payload;
            }
        }

        return bestScore > 0 ? best : null;
    }

    private static ReferenceGraphPayload? Pick(IReadOnlyList<ReferenceGraphPayload> references, string id)
    {
        var hit = references.FirstOrDefault(r => string.Equals(r.id, id, StringComparison.OrdinalIgnoreCase));
        if (hit != null)
        {
            return Clone(hit);
        }

        return LoadSafe(id);
    }

    private static ReferenceGraphPayload Clone(ReferenceGraphPayload source) =>
        JsonConvert.DeserializeObject<ReferenceGraphPayload>(JsonConvert.SerializeObject(source))
        ?? source;

    private static Func<LogicNode, bool> NodeMatchesBinding(string bindingId)
    {
        var member = bindingId.Split('.').LastOrDefault() ?? bindingId;
        return node =>
        {
            if (!node.parameters.TryGetValue("bindingId", out var bound))
            {
                return false;
            }

            return string.Equals(bound, bindingId, StringComparison.OrdinalIgnoreCase)
                   || bound.Contains(member, StringComparison.OrdinalIgnoreCase);
        };
    }

    private static ReferenceGraphPayload? LoadSafe(string id)
    {
        try
        {
            return LogicProjectStore.LoadReference(id);
        }
        catch
        {
            return null;
        }
    }
}
