using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public class GameBindingValueSchemaTests
{
    [Theory]
    [InlineData("Boolean", LogicParamKind.Bool)]
    [InlineData("bool", LogicParamKind.Bool)]
    [InlineData("String", LogicParamKind.Text)]
    [InlineData("Single", LogicParamKind.Number)]
    [InlineData("Int32", LogicParamKind.Number)]
    public void ClassifyClrType_Maps_Primitives(string clr, LogicParamKind expected) =>
        Assert.Equal(expected, GameBindingValueSchema.ClassifyClrType(clr));

    [Fact]
    public void ResolveExpectValueKind_Uses_SourceClrType_For_BoolBinding()
    {
        var graph = new LogicGraph
        {
            nodes =
            [
                new LogicNode
                {
                    id = "s1",
                    kind = "source",
                    typeId = "Member.Bind",
                    parameters = new Dictionary<string, string>
                    {
                        ["bindingId"] = "Member.Pilot.onEject",
                        [GameBindingValueSchema.ClrTypeParameterKey] = "Boolean"
                    }
                },
                new LogicNode
                {
                    id = "c1",
                    kind = "check",
                    typeId = "Compare.Equals",
                    parameters = new Dictionary<string, string> { ["watchParam"] = "Member.Pilot.onEject" }
                }
            ],
            edges = [new LogicEdge { fromNode = "s1", toNode = "c1", fromPort = "out", toPort = "in" }]
        };

        var check = graph.nodes.First(n => n.id == "c1");
        Assert.Equal(LogicParamKind.Bool, GameBindingValueSchema.ResolveExpectValueKind(check, graph));
    }

    [Fact]
    public void ApplyDefaultExpectForKind_Sets_True_For_Bool()
    {
        var check = new LogicNode
        {
            kind = "check",
            typeId = "Compare.Equals",
            parameters = new Dictionary<string, string>
            {
                ["watchParam"] = "Member.Pilot.onEject",
                [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
            }
        };

        GameBindingValueSchema.ApplyDefaultExpectForKind(check);
        Assert.Equal("true", check.parameters["expectValue"]);
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("1", "true")]
    [InlineData("да", "true")]
    public void TryNormalizeExpectValue_Accepts_Bool_Literals(string raw, string expected)
    {
        Assert.True(GameBindingValueSchema.TryNormalizeExpectValue(raw, LogicParamKind.Bool, out var normalized));
        Assert.Equal(expected, normalized);
    }
}

public class LogicParamCatalogWatchParamTests
{
    [Fact]
    public void ResolveUpstreamWatchParam_Returns_BindingId_For_MemberBind()
    {
        var graph = new LogicGraph
        {
            nodes =
            [
                new LogicNode
                {
                    id = "s1",
                    kind = "source",
                    typeId = "Member.Bind",
                    parameters = new Dictionary<string, string>
                    {
                        ["bindingId"] = "Member.Pilot.onEject",
                        ["displayName"] = "onEject"
                    }
                },
                new LogicNode
                {
                    id = "c1",
                    kind = "check",
                    typeId = "Compare.GreaterThan",
                    parameters = new Dictionary<string, string>()
                }
            ],
            edges = [new LogicEdge { fromNode = "s1", toNode = "c1", fromPort = "out", toPort = "in" }]
        };

        var check = graph.nodes.First(n => n.id == "c1");
        var watch = LogicParamCatalog.ResolveUpstreamWatchParam(check, graph);
        Assert.Equal("Member.Pilot.onEject", watch);

        var choices = LogicParamCatalog.WatchParamChoicesForNode(check, graph);
        Assert.Single(choices);
        Assert.Equal("Member.Pilot.onEject", choices[0]);
    }

    [Fact]
    public void TryAutoFillWatchParamFromEdge_Fills_Empty_WatchParam()
    {
        var graph = new LogicGraph
        {
            nodes =
            [
                new LogicNode
                {
                    id = "s1",
                    kind = "source",
                    typeId = "Member.Bind",
                    parameters = new Dictionary<string, string> { ["bindingId"] = "Member.Pilot.onEject" }
                },
                new LogicNode
                {
                    id = "c1",
                    kind = "check",
                    typeId = "Compare.Equals",
                    parameters = new Dictionary<string, string>()
                }
            ],
            edges = Array.Empty<LogicEdge>()
        };

        var edge = new LogicEdge { fromNode = "s1", toNode = "c1", fromPort = "out", toPort = "in" };
        graph.edges = graph.edges.Append(edge).ToArray();
        LogicParamCatalog.TryAutoFillWatchParamFromEdge(graph, edge);

        var check = graph.nodes.First(n => n.id == "c1");
        Assert.Equal("Member.Pilot.onEject", check.parameters["watchParam"]);
    }

    [Fact]
    public void WatchParamFriendlyTitle_Uses_DisplayName_From_Source()
    {
        var graph = new LogicGraph
        {
            nodes =
            [
                new LogicNode
                {
                    id = "s1",
                    kind = "source",
                    typeId = "Member.Bind",
                    parameters = new Dictionary<string, string>
                    {
                        ["bindingId"] = "Member.Pilot.onEject",
                        ["displayName"] = "Ejection"
                    }
                }
            ],
            edges = Array.Empty<LogicEdge>()
        };

        Assert.Equal("Ejection", LogicParamCatalog.WatchParamFriendlyTitle("Member.Pilot.onEject", graph));
    }
}
