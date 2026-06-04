using NuclearOptionSDK.ModKit;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class MsBuildLocatorTests
{
    [Fact]
    public void TryResolve_returns_existing_msbuild_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = MsBuildLocator.TryResolve();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(File.Exists(path!), path);
        Assert.EndsWith("MSBuild.exe", path, StringComparison.OrdinalIgnoreCase);
    }
}
