using NuclearOptionSDK.GameBindings;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class GameBindingRuntimeTests
{
    [Fact]
    public void ReadBool_matches_parameterless_method_case_insensitively()
    {
        var aircraft = new FakeAircraft();
        aircraft.GearDeployed = true;

        Assert.True(GameBindingRuntime.ReadBool(aircraft, "Member.Aircraft.isLanded"));
        Assert.False(GameBindingRuntime.ReadBool(aircraft, "Member.Aircraft.isSeated"));
    }

    [Fact]
    public void ApplyWrite_gearDeployed_calls_SetGear()
    {
        var aircraft = new FakeAircraft();
        aircraft.GearDeployed = true;

        Assert.True(GameBindingRuntime.ApplyWrite(aircraft, "Member.Aircraft.gearDeployed", false));
        Assert.False(aircraft.GearDeployed);
        Assert.Equal(1, aircraft.SetGearCallCount);
    }

    private sealed class FakeAircraft
    {
        public bool GearDeployed { get; set; }

        public int SetGearCallCount { get; private set; }

        public bool IsLanded() => true;

        public bool isSeated => false;

        public void SetGear(bool deployed)
        {
            SetGearCallCount++;
            GearDeployed = deployed;
        }
    }
}
