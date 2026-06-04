using NuclearOptionSDK.Decompiler;
using Xunit;

namespace NuclearOptionSDK.Decompiler.Tests;

public sealed class GameInstallValidatorTests
{
    [Fact]
    public void Empty_path_invalid()
    {
        var result = GameInstallValidator.Validate(null);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Missing_folder_invalid()
    {
        var result = GameInstallValidator.Validate(@"C:\no-such-nuclear-option-folder-xyz");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Real_install_valid_when_present()
    {
        var root = Environment.GetEnvironmentVariable("NO_GAME_ROOT")
                   ?? @"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option";
        if (!Directory.Exists(root))
        {
            return;
        }

        var result = GameInstallValidator.Validate(root);
        Assert.True(result.IsValid);
        Assert.NotNull(result.AssemblyPath);
        Assert.True(File.Exists(result.AssemblyPath));
    }
}
