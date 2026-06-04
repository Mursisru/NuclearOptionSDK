using NuclearOptionSDK.ModKit;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class GameAssemblyReferenceCollectorTests
{
    [Fact]
    public void Collect_returns_sorted_unique_assembly_names()
    {
        var root = Path.Combine(Path.GetTempPath(), "nosdk-ref-test-" + Guid.NewGuid().ToString("N"));
        var managed = Path.Combine(root, "NuclearOption_Data", "Managed");
        var bep = Path.Combine(root, "BepInEx", "core");
        Directory.CreateDirectory(managed);
        Directory.CreateDirectory(bep);
        File.WriteAllBytes(Path.Combine(managed, "UnityEngine.dll"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(managed, "Assembly-CSharp.dll"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(bep, "BepInEx.dll"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(bep, "0Harmony.dll"), Array.Empty<byte>());

        try
        {
            var refs = GameAssemblyReferenceCollector.Collect(root);
            Assert.Equal(4, refs.Count);
            Assert.Contains(refs, r => r.AssemblyName == "UnityEngine");
            Assert.Contains(refs, r => r.AssemblyName == "BepInEx");
            Assert.Equal(
                new[] { "0Harmony", "Assembly-CSharp", "BepInEx", "UnityEngine" },
                refs.Select(r => r.AssemblyName).ToArray());
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
                // best effort
            }
        }
    }

    [Fact]
    public void Build_csproj_lists_all_collected_references()
    {
        var refs = new[]
        {
            new GameAssemblyReferenceCollector.GameAssemblyReference("BepInEx", @"C:\Game\BepInEx\core\BepInEx.dll"),
            new GameAssemblyReferenceCollector.GameAssemblyReference("UnityEngine", @"C:\Game\Managed\UnityEngine.dll")
        };

        var xml = ModCsprojTemplate.Build("test_Engine", @"C:\Game", ["testPlugin.cs"], refs);
        Assert.Contains(@"Reference Include=""BepInEx""", xml);
        Assert.Contains(@"Reference Include=""UnityEngine""", xml);
        Assert.Contains(@"C:\Game\BepInEx\core\BepInEx.dll", xml);
        Assert.DoesNotContain("Microsoft.NET.Sdk", xml);
    }
}
