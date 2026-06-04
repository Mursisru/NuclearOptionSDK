namespace NuclearOptionSDK.Studio.Services.ApiSurface.Models;

/// <summary>How a member behaves in the game API (UI grouping).</summary>
public enum MemberBehaviorBucket
{
    /// <summary>Method / constructor — performs an action; signature includes ().</summary>
    Action,

    /// <summary>Field or property holding a scalar (int, float, string, bool, enum).</summary>
    Data,

    /// <summary>Field or property referencing a game object type (Player, Aircraft, …).</summary>
    Reference
}
