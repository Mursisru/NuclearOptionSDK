using NuclearOptionSDK.ModKit;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class ModCsprojTemplateTests
{
    [Fact]
    public void Build_uses_non_sdk_csproj_without_nuget_restore()
    {
        var refs = new[]
        {
            new GameAssemblyReferenceCollector.GameAssemblyReference("BepInEx", @"C:\Games\Nuclear Option\BepInEx\core\BepInEx.dll")
        };
        var xml = ModCsprojTemplate.Build(
            "test_Engine",
            @"C:\Games\Nuclear Option",
            ["testPlugin.cs"],
            refs);

        Assert.DoesNotContain("Microsoft.NET.Sdk", xml);
        Assert.Contains("TargetFrameworkVersion>v4.8<", xml);
        Assert.Contains(@"testPlugin.cs", xml);
        Assert.DoesNotContain("project.assets.json", xml);
    }
}
