using System.Collections.Generic;
using NuclearOptionSDK.LogicCore;
using NuclearOptionSDK.Protocol;
using Xunit;

namespace NuclearOptionSDK.LogicCore.Tests;

public sealed class LogicGraphEvaluatorTests
{
    [Fact]
    public void AoA_above_15_sets_red_overlay()
    {
        var project = new LogicProject
        {
            mergeMode = "all",
            userGraph = new LogicGraph
            {
                nodes = new[]
                {
                    new LogicNode { id = "src", kind = "source", typeId = "Telemetry.AoA" },
                    new LogicNode { id = "chk", kind = "check", typeId = "Compare.GreaterThan", parameters = new Dictionary<string, string> { ["threshold"] = "15" } },
                    new LogicNode { id = "out", kind = "output", typeId = "Action.SetOverlayColor", parameters = new Dictionary<string, string> { ["labelId"] = "aoa-label", ["colorHtml"] = "#FF0000" } }
                },
                edges = new[]
                {
                    new LogicEdge { fromNode = "src", toNode = "chk" },
                    new LogicEdge { fromNode = "chk", toNode = "out" }
                }
            }
        };

        var telemetry = new DictionaryTelemetryContext(new Dictionary<string, double> { ["Telemetry.AoA"] = 20 });
        var evaluator = new LogicGraphEvaluator();
        var result = evaluator.Evaluate(project, telemetry);

        Assert.Single(result.Actions);
        Assert.Equal("#FF0000", result.Actions[0].colorHtml);
        Assert.Equal("aoa-label", result.Actions[0].labelId);
    }

    [Fact]
    public void AoA_below_threshold_produces_no_actions()
    {
        var project = new LogicProject
        {
            userGraph = new LogicGraph
            {
                nodes = new[]
                {
                    new LogicNode { id = "src", kind = "source", typeId = "Telemetry.AoA" },
                    new LogicNode { id = "chk", kind = "check", typeId = "Compare.GreaterThan", parameters = new Dictionary<string, string> { ["threshold"] = "15" } },
                    new LogicNode { id = "out", kind = "output", typeId = "Action.SetOverlayColor", parameters = new Dictionary<string, string> { ["labelId"] = "aoa-label", ["colorHtml"] = "#FF0000" } }
                },
                edges = new[]
                {
                    new LogicEdge { fromNode = "src", toNode = "chk" },
                    new LogicEdge { fromNode = "chk", toNode = "out" }
                }
            }
        };

        var telemetry = new DictionaryTelemetryContext(new Dictionary<string, double> { ["Telemetry.AoA"] = 10 });
        var result = new LogicGraphEvaluator().Evaluate(project, telemetry);
        Assert.Empty(result.Actions);
    }
}
