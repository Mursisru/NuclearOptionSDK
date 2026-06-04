using NuclearOptionSDK.Studio.Services;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class ActionMethodLabelTests
{
    [Theory]
    [InlineData("PauseGame()", "PauseGame")]
    [InlineData("PauseGame", "PauseGame")]
    [InlineData("  Fire Weapon ()  ", "Fire Weapon")]
    public void StripCallSuffix_removes_trailing_empty_parens(string input, string expected) =>
        Assert.Equal(expected, ActionMethodLabel.StripCallSuffix(input));
}
