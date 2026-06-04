using NuclearOptionSDK.Studio.Services;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class LogicParamCatalogDefaultBindingTests
{
    [Theory]
    [InlineData("Gate.OnlyWhenInFlight", "Read.Aircraft.speed")]
    [InlineData("Gate.WhileAirborne", "Read.Unit.radarAlt")]
    [InlineData("Gate.WhileOnGround", "Read.Unit.radarAlt")]
    [InlineData("Gate.FuelLow", "Read.Aircraft.fuelLevel")]
    [InlineData("Gate.GearDown", "Member.Aircraft.gearDeployed")]
    public void ResolveDefaultWatchBindingForCheck_maps_legacy_gates(string checkTypeId, string expectedBinding)
    {
        var binding = LogicParamCatalog.ResolveDefaultWatchBindingForCheck(checkTypeId);
        Assert.Equal(expectedBinding, binding);
    }

    [Fact]
    public void ResolveDefaultWatchBindingForCheck_returns_null_for_unknown_gate()
    {
        Assert.Null(LogicParamCatalog.ResolveDefaultWatchBindingForCheck("Gate.UnknownThing"));
    }
}
