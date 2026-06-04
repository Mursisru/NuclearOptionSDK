using System.Collections.Generic;
using System.Linq;
using NuclearOptionSDK.LogicCore;
using NuclearOptionSDK.Protocol;
using Xunit;

namespace NuclearOptionSDK.LogicCore.Tests;

public sealed class LogicMemberWriteTests
{
    [Fact]
    public void EvaluateOutput_Emits_SetMemberBind_When_memberWriteOn()
    {
        var node = new LogicNode
        {
            kind = "output",
            typeId = "Node.Output",
            parameters = new Dictionary<string, string>
            {
                ["memberWriteOn"] = "true",
                ["memberWriteBindingId"] = "Member.Aircraft.gearDeployed",
                ["memberWriteValue"] = "false"
            }
        };

        var actions = LogicGraphEvaluator.EvaluateOutput(node).ToList();
        Assert.Single(actions);
        Assert.Equal("Action.SetMemberBind", actions[0].typeId);
        Assert.Equal("Member.Aircraft.gearDeployed", actions[0].labelId);
        Assert.Equal("false", actions[0].text);
    }
}
