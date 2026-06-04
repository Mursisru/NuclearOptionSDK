namespace NuclearOptionSDK.Studio.Services;

/// <summary>Compare operators for the Check palette. Parameter and threshold are in the inspector on the right.</summary>
public static class LogicCheckCatalog
{
    public sealed record CheckDef(string Id, string Title, string Hint);

    /// <summary>Compare.* only — shown on the Check tab.</summary>
    public static readonly CheckDef[] Palette =
    [
        new("Compare.GreaterThan", "Greater than threshold", "value > threshold"),
        new("Compare.GreaterOrEqual", "Greater or equal", "value ≥ threshold"),
        new("Compare.LessThan", "Less than threshold", "value < threshold"),
        new("Compare.LessOrEqual", "Less or equal", "value ≤ threshold"),
        new("Compare.Equals", "Equals", "value == threshold"),
        new("Compare.NotEquals", "Not equals", "value != threshold"),
        new("Compare.InRange", "In range", "min ≤ value ≤ max"),
        new("Compare.OutsideRange", "Outside range", "value < min or > max"),
        new("Compare.CrossedAbove", "Crossed above threshold", "became greater (once)"),
        new("Compare.CrossedBelow", "Crossed below threshold", "became less (once)"),
        new("Compare.Hysteresis", "With hysteresis", "different on/off thresholds"),
        new("Compare.Debounce", "Hold N seconds", "true for at least N sec"),
        new("Compare.Changed", "Changed", "differs from previous frame"),
        new("Compare.StableFor", "Stable N sec", "unchanged for N seconds"),
        new("Compare.RateLimit", "No more than N/sec", "rate limit"),
        new("Compare.IsTrue", "True (bool/≠0)", "value is truthy"),
        new("Compare.IsFalse", "False (bool/0)", "value is falsy"),
        new("Compare.Approximately", "Approximately equal", "|value − threshold| ≤ ε")
    ];

    /// <summary>Legacy Gate.* — old graphs only, not in palette.</summary>
    private static readonly CheckDef[] LegacyGates =
    [
        new("Gate.OnlyWhenInFlight", "Only in flight", "speed > threshold"),
        new("Gate.OnlyWhen", "Only when", "nested check"),
        new("Gate.HideWhen", "Hide when", "invert OnlyWhen"),
        new("Gate.WhileAirborne", "Airborne", "radarAlt > threshold"),
        new("Gate.WhileOnGround", "On ground", "radarAlt ≤ threshold"),
        new("Gate.WeaponSelected", "Weapon selected", "active station"),
        new("Gate.FuelLow", "Low fuel", "fuel < threshold"),
        new("Gate.GearUp", "Gear up", "gear retracted"),
        new("Gate.GearDown", "Gear down", "gear extended"),
        new("Gate.WhilePaused", "Paused", "timeScale == 0"),
        new("Gate.DelayBeforeShow", "Delay", "true after N sec"),
        new("Gate.Cooldown", "Cooldown", "no more than every N sec"),
        new("Gate.BlinkWhileTrue", "Blink", "period while true"),
        new("Gate.ShowOnlyOnce", "Once", "latch"),
        new("Gate.AutoHideAfter", "Auto hide", "after N sec"),
        new("Gate.Priority", "Priority", "higher wins")
    ];

    public static CheckDef[] All => Palette.Concat(LegacyGates).ToArray();

    public static bool IsPaletteCheck(string typeId) =>
        typeId.StartsWith("Compare.", StringComparison.Ordinal)
        && Palette.Any(c => string.Equals(c.Id, typeId, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnownCheck(string typeId) =>
        All.Any(c => string.Equals(c.Id, typeId, StringComparison.OrdinalIgnoreCase));

    public static CheckDef Resolve(string typeId)
    {
        var found = All.FirstOrDefault(c => string.Equals(c.Id, typeId, StringComparison.OrdinalIgnoreCase));
        return found ?? new CheckDef(typeId, typeId, typeId);
    }

    public static string Title(string typeId) => Resolve(typeId).Title;

    public static string Hint(string typeId) => Resolve(typeId).Hint;
}
