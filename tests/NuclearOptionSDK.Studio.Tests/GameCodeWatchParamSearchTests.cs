using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class GameCodeWatchParamSearchTests
{
    [Fact]
    public void SearchWatchParamIds_Finds_GameCode_Bool_Field()
    {
        GameCodeIndexCache.SetIndex(
        [
            new GameTypeNode
            {
                FullName = "Aircraft",
                ShortName = "Aircraft",
                Parameters =
                [
                    new GameMemberNode
                    {
                        Kind = GameMemberKind.Parameter,
                        Behavior = MemberBehaviorBucket.Data,
                        Name = "isLanded",
                        Signature = "Boolean isLanded",
                        BindingId = "Member.Aircraft.isLanded",
                        ClrTypeName = "Boolean"
                    }
                ],
                Methods = [],
                Values = [],
                Category = ApiSurfaceCategory.FlightTelemetry,
                OwnerContext = OwnerContextKind.Flight
            }
        ]);

        var check = new LogicNode { id = "c1", kind = "check", typeId = "Compare.Equals" };
        var hits = LogicParamCatalog.SearchWatchParamIds("isLanded", check, null);

        Assert.Contains("Member.Aircraft.isLanded", hits);
    }

    [Fact]
    public void ApplyWatchParamMetadata_Copies_Boolean_From_Index()
    {
        GameCodeIndexCache.SetIndex(
        [
            new GameTypeNode
            {
                FullName = "Aircraft",
                ShortName = "Aircraft",
                Parameters =
                [
                    new GameMemberNode
                    {
                        Kind = GameMemberKind.Parameter,
                        Behavior = MemberBehaviorBucket.Data,
                        Name = "isLanded",
                        Signature = "Boolean isLanded",
                        BindingId = "Member.Aircraft.isLanded",
                        ClrTypeName = "bool"
                    }
                ],
                Methods = [],
                Values = [],
                Category = ApiSurfaceCategory.FlightTelemetry,
                OwnerContext = OwnerContextKind.Flight
            }
        ]);

        var check = new LogicNode
        {
            kind = "check",
            typeId = "Compare.Equals",
            parameters = new Dictionary<string, string> { ["watchParam"] = "Member.Aircraft.isLanded" }
        };

        GameBindingValueSchema.ApplyWatchParamMetadata(check);
        Assert.Equal("bool", check.parameters[GameBindingValueSchema.ClrTypeParameterKey]);
        Assert.Equal(LogicParamKind.Bool, GameBindingValueSchema.ResolveExpectValueKind(check));
    }
}
