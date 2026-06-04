using NuclearOptionSDK.Decompiler;
using Xunit;

namespace NuclearOptionSDK.Decompiler.Tests;

public sealed class IlSpyDecompileServiceTests
{
    [Fact]
    public async Task Decompile_method_when_game_root_set()
    {
        var root = Environment.GetEnvironmentVariable("NO_GAME_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var main = GameAssemblyPaths.ResolveMainAssembly(root);
        if (main == null)
        {
            return;
        }

        var service = new IlSpyDecompileService();
        var code = await service.DecompileMethodAsync(root, "Aircraft", "Update");
        if (string.IsNullOrWhiteSpace(code))
        {
            code = await service.DecompileMethodAsync(root, "Aircraft", "Awake");
        }

        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.True(code!.Length > 80, "Decompiled body too short.");
        Assert.Contains("{", code, StringComparison.Ordinal);
    }
}
