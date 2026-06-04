using NuclearOptionSDK.Studio.Services.ApiSurface;
using NuclearOptionSDK.Studio.Services.ApiSurface.Filtering;
using NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;
using NuclearOptionSDK.Studio.Services.ApiSurface.Repl;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class ApiSurfaceTests
{
    private static string? GameRoot
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("NO_GAME_ROOT");
            if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            {
                return env;
            }

            const string steam = @"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option";
            return Directory.Exists(steam) ? steam : null;
        }
    }

    [Fact]
    public void MemberBindingId_is_stable()
    {
        var id = ApiSymbolIdFactory.MemberBindingId("Aircraft", "speed");
        Assert.Equal("Member.Aircraft.speed", id);
    }

    [Fact]
    public void TryResolveReadId_returns_member_when_not_curated()
    {
        var id = ApiSymbolIdFactory.TryResolveReadId("Aircraft", "unknownFieldXYZ");
        Assert.Equal("Member.Aircraft.unknownFieldXYZ", id);
    }

    [Fact]
    public void CompilerNoiseFilter_hides_backing_fields()
    {
        var filter = new CompilerNoiseFilter();
        var member = new ApiMemberModel
        {
            Id = new ApiSymbolId("T", ApiMemberKind.Field, "<x>k__BackingField"),
            TechnicalName = "<x>k__BackingField",
            Signature = "int <x>k__BackingField",
            ClrTypeName = "int",
            Source = ApiMemberSource.Declared,
            Category = ApiSurfaceCategory.General,
            IsHidden = false,
            PriorityScore = 0,
            BindingId = "Member.T.<x>k__BackingField"
        };
        var type = new ApiTypeModel
        {
            FullName = "T",
            Namespace = "",
            Name = "T",
            BaseTypeFullName = null,
            IsEnum = false,
            PriorityScore = 0,
            DisplayTag = null,
            Category = ApiSurfaceCategory.General,
            Members = Array.Empty<ApiMemberModel>()
        };

        Assert.True(filter.ShouldHide(member, type, new ApiSurfaceRules()));
    }

    [Fact]
    public void HumanizeLabelResolver_splits_camel_case()
    {
        var resolver = new HumanizeLabelResolver();
        Assert.True(resolver.TryResolveMember("Aircraft", "fuelLevel", out var label));
        Assert.Contains("fuel", label.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplSurfaceTranslator_replaces_aliases()
    {
        var translator = new ReplSurfaceTranslator();
        var result = translator.Translate("var x = current_aircraft;");
        Assert.Contains("Aircraft", result.TechnicalSource);
    }

    [Fact]
    public void ApiSurfaceIndex_contains_Aircraft_when_game_installed()
    {
        if (GameRoot == null)
        {
            return;
        }

        var types = ApiSurfaceIndex.Build(GameRoot);
        Assert.Contains(types, t => t.Name == "Aircraft");
        var aircraft = types.First(t => t.Name == "Aircraft");
        Assert.Contains(aircraft.Members, m =>
            m.TechnicalName is "speed" or "fuelLevel" or "gForce");
    }

    [Fact]
    public void ApiSurfaceIndex_hides_compiler_types_when_game_installed()
    {
        if (GameRoot == null)
        {
            return;
        }

        var types = ApiSurfaceIndex.Build(GameRoot);
        Assert.DoesNotContain(types, t => t.FullName.Contains("PrivateImplementationDetails", StringComparison.Ordinal));
    }
}
