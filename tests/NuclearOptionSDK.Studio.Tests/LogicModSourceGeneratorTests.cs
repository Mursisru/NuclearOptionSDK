using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;
using NuclearOptionSDK.Studio.Services.ApiSurface.Context;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class LogicModSourceGeneratorTests
{
    [Fact]
    public void Generate_includes_plugin_class_and_member_write()
    {
        SeedAircraftIndex();
        var project = new LogicProject
        {
            name = "test",
            userGraph = new LogicGraph
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
                            ["bindingId"] = "Member.Aircraft.isSeated"
                        }
                    },
                    new LogicNode
                    {
                        id = "c1",
                        kind = "check",
                        typeId = "Compare.Equals",
                        parameters = new Dictionary<string, string>
                        {
                            ["watchParam"] = "Member.Aircraft.isSeated",
                            ["expectValue"] = "false",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    },
                    new LogicNode
                    {
                        id = "o1",
                        kind = "output",
                        typeId = "Node.Output",
                        parameters = new Dictionary<string, string>
                        {
                            [LogicOutputMemberWrite.OnKey] = "true",
                            [LogicOutputMemberWrite.BindingKey] = "Member.Aircraft.gearDeployed",
                            [LogicOutputMemberWrite.ValueKey] = "false",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    }
                ],
                edges =
                [
                    new LogicEdge { fromNode = "s1", toNode = "c1", fromPort = "out", toPort = "in" },
                    new LogicEdge { fromNode = "c1", toNode = "o1", fromPort = "out", toPort = "in" }
                ]
            }
        };

        var code = LogicModSourceGenerator.Generate(project);

        Assert.Contains("BepInPlugin", code);
        Assert.Contains("LogicModRuntime", code);
        Assert.Contains("EvaluateUserGraph", code);
        Assert.Contains("aircraft.isSeated", code);
        Assert.Contains("aircraft.SetGear(false)", code);
        Assert.Contains("GameManager.GetLocalAircraft(out Aircraft aircraft)", code);
        Assert.DoesNotContain("private static bool TryGetLocalAircraft", code);
        Assert.DoesNotContain("private static void WriteMember", code);
    }

    [Fact]
    public void Generate_four_node_chain_uses_invariant_tick_hud_and_typed_member_writes()
    {
        SeedAircraftIndex();
        const string gearBinding = "Member.Aircraft.gearDeployed";
        var project = new LogicProject
        {
            name = "gear-hud",
            tickRateHz = 10,
            userGraph = new LogicGraph
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
                            ["bindingId"] = "Member.Aircraft.isLanded",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    },
                    new LogicNode
                    {
                        id = "c1",
                        kind = "check",
                        typeId = "Compare.Equals",
                        parameters = new Dictionary<string, string>
                        {
                            ["watchParam"] = "Member.Aircraft.isLanded",
                            ["expectValue"] = "false",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    },
                    new LogicNode
                    {
                        id = "c2",
                        kind = "check",
                        typeId = "Compare.Equals",
                        parameters = new Dictionary<string, string>
                        {
                            ["watchParam"] = "Member.Aircraft.gearDown",
                            ["expectValue"] = "true",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    },
                    new LogicNode
                    {
                        id = "o1",
                        kind = "output",
                        typeId = "Node.Output",
                        parameters = new Dictionary<string, string>
                        {
                            [LogicOutputChangeCatalog.OnKey("visible")] = "true",
                            [LogicOutputChangeCatalog.ValKey("visible")] = "true",
                            [LogicOutputChangeCatalog.OnKey("color")] = "true",
                            [LogicOutputChangeCatalog.ValKey("color")] = "#FF4400",
                            [LogicOutputChangeCatalog.OnKey("text")] = "true",
                            [LogicOutputChangeCatalog.ValKey("text")] = "GEAR DOWN",
                            [LogicOutputChangeCatalog.OnKey(gearBinding)] = "true",
                            [LogicOutputChangeCatalog.ValKey(gearBinding)] = "false",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    }
                ],
                edges =
                [
                    new LogicEdge { fromNode = "s1", toNode = "c1", fromPort = "out", toPort = "in" },
                    new LogicEdge { fromNode = "c1", toNode = "c2", fromPort = "out", toPort = "in" },
                    new LogicEdge { fromNode = "c2", toNode = "o1", fromPort = "out", toPort = "in" }
                ]
            }
        };

        var code = LogicModSourceGenerator.Generate(project);

        Assert.Contains("TickInterval = 0.100f", code);
        Assert.DoesNotContain("TickInterval = 0,100f", code);
        Assert.DoesNotContain("GameBindingRuntime.ApplyWrite(aircraft, \"Member.Aircraft.gearDeployed\", \"\")", code);
        Assert.DoesNotMatch(@"GameBindingRuntime\.ApplyWrite\(aircraft, ""[^""]+"", """"\)", code);
        Assert.Contains("== false;", code);
        Assert.Contains("== true;", code);
        Assert.Contains("aircraft.isLanded", code);
        Assert.Contains("aircraft.gearDown", code);
        Assert.Contains("// HUD visibility", code);
        Assert.Contains("// HUD overlay color", code);
        Assert.Contains("// HUD text", code);
        Assert.Contains("aircraft.SetGear(false)", code);
        Assert.Contains("// Nodes: s1:Member.Bind,c1:Compare.Equals,c2:Compare.Equals,o1:Node.Output", code);
    }

    [Fact]
    public void Generate_changed_check_emits_evaluate_changed_and_hud_color_output()
    {
        var project = new LogicProject
        {
            tickRateHz = 10,
            userGraph = new LogicGraph
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
                            ["bindingId"] = "Member.Aircraft.isLanded",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    },
                    new LogicNode
                    {
                        id = "c1",
                        kind = "check",
                        typeId = "Compare.Changed",
                        parameters = new Dictionary<string, string>
                        {
                            ["watchParam"] = "Member.Aircraft.isLanded",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    },
                    new LogicNode
                    {
                        id = "o1",
                        kind = "output",
                        typeId = "Action.SetHudColor",
                        parameters = new Dictionary<string, string>
                        {
                            [LogicOutputChangeCatalog.OnKey("color")] = "true",
                            [LogicOutputChangeCatalog.ValKey("color")] = "#FF0000"
                        }
                    }
                ],
                edges =
                [
                    new LogicEdge { fromNode = "s1", toNode = "c1", fromPort = "out", toPort = "in" },
                    new LogicEdge { fromNode = "c1", toNode = "o1", fromPort = "out", toPort = "in" }
                ]
            }
        };

        var code = LogicModSourceGenerator.Generate(project);

        Assert.Matches(@"EvaluateChanged\(""c1"", value_\w+\)", code);
        Assert.DoesNotContain("bool ok = value == true;", code);
        Assert.Contains("// HUD overlay color → #FF0000", code);
        Assert.Contains("// Edges: s1->c1, c1->o1", code);
        Assert.Contains("TickInterval = 0.100f", code);
    }

    [Fact]
    public void Generate_airborne_gate_reads_radar_alt_above_threshold()
    {
        SeedAircraftIndex();
        var project = new LogicProject
        {
            userGraph = new LogicGraph
            {
                nodes =
                [
                    new LogicNode
                    {
                        id = "s1",
                        kind = "source",
                        typeId = "Member.Bind",
                        parameters = new Dictionary<string, string> { ["bindingId"] = "Member.Aircraft.IsLanded" }
                    },
                    new LogicNode
                    {
                        id = "c1",
                        kind = "check",
                        typeId = "Gate.WhileAirborne",
                        parameters = new Dictionary<string, string>
                        {
                            ["threshold"] = "1",
                            ["watchParam"] = "Member.Aircraft.radarAlt"
                        }
                    },
                    new LogicNode
                    {
                        id = "o1",
                        kind = "output",
                        typeId = "Node.Output",
                        parameters = new Dictionary<string, string>
                        {
                            [LogicOutputMemberWrite.OnKey] = "true",
                            [LogicOutputMemberWrite.BindingKey] = "Member.Aircraft.gearDeployed",
                            [LogicOutputMemberWrite.ValueKey] = "false"
                        }
                    }
                ],
                edges =
                [
                    new LogicEdge { fromNode = "s1", toNode = "c1", fromPort = "out", toPort = "in" },
                    new LogicEdge { fromNode = "c1", toNode = "o1", fromPort = "out", toPort = "in" }
                ]
            }
        };

        var code = LogicModSourceGenerator.Generate(project);

        Assert.Contains("aircraft.radarAlt", code);
        Assert.Matches(@"bool ok_\w+ = value_\w+ > 1f;", code);
    }

    [Fact]
    public void Generate_patch_mode_emits_harmony_patch_and_trigger()
    {
        SeedAircraftIndex();
        var project = new LogicProject
        {
            executionMode = "Patch",
            patchTargetType = "Aircraft",
            patchMethodName = "Update",
            patchKind = "Postfix",
            userGraph = new LogicGraph()
        };

        var code = LogicModSourceGenerator.Generate(project, "PatchLogic", "com.test.patchlogic");

        Assert.Contains("[HarmonyPatch(typeof(Aircraft), \"Update\")]", code);
        Assert.Contains("public static void Postfix()", code);
        Assert.Contains("PatchLogicRuntime.TriggerImmediateTick();", code);
    }

    [Fact]
    public void Generate_legacy_in_flight_gate_without_watchParam_uses_catalog_speed()
    {
        SeedAircraftIndex();
        var project = new LogicProject
        {
            userGraph = new LogicGraph
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
                            ["bindingId"] = "Member.Aircraft.isLanded",
                            [GameBindingValueSchema.ClrTypeParameterKey] = "bool"
                        }
                    },
                    new LogicNode
                    {
                        id = "c1",
                        kind = "check",
                        typeId = "Gate.OnlyWhenInFlight",
                        parameters = new Dictionary<string, string> { ["threshold"] = "12" }
                    },
                    new LogicNode
                    {
                        id = "o1",
                        kind = "output",
                        typeId = "Node.Output",
                        parameters = new Dictionary<string, string>
                        {
                            [LogicOutputMemberWrite.OnKey] = "true",
                            [LogicOutputMemberWrite.BindingKey] = "Member.Aircraft.gearDeployed",
                            [LogicOutputMemberWrite.ValueKey] = "false"
                        }
                    }
                ],
                edges =
                [
                    new LogicEdge { fromNode = "s1", toNode = "c1", fromPort = "out", toPort = "in" },
                    new LogicEdge { fromNode = "c1", toNode = "o1", fromPort = "out", toPort = "in" }
                ]
            }
        };

        var code = LogicModSourceGenerator.Generate(project);

        Assert.True(
            code.Contains("aircraft.speed", StringComparison.Ordinal)
            || code.Contains("Read.Aircraft.speed", StringComparison.Ordinal),
            "Expected typed or catalog speed binding in generated code.");
        Assert.DoesNotContain("missing-binding", code);
    }

    [Fact]
    public void Generate_diagnostic_mode_emits_sdk_diag_logger()
    {
        SeedAircraftIndex();
        var project = new LogicProject
        {
            diagnosticMode = true,
            userGraph = new LogicGraph()
        };

        var code = LogicModSourceGenerator.Generate(project);

        Assert.Contains("private const bool DiagnosticEnabled = true;", code);
        Assert.Contains("Debug.Log(\"[SDK-DIAG] \" + message);", code);
    }

    private static void SeedAircraftIndex()
    {
        GameCodeIndexCache.SetIndex(
        [
            new GameTypeNode
            {
                FullName = "Aircraft",
                ShortName = "Aircraft",
                Category = ApiSurfaceCategory.FlightTelemetry,
                OwnerContext = OwnerContextKind.Flight,
                Parameters =
                [
                    new GameMemberNode
                    {
                        Kind = GameMemberKind.Parameter,
                        Behavior = MemberBehaviorBucket.Data,
                        Name = "speed",
                        Signature = "float speed",
                        BindingId = "Member.Aircraft.speed",
                        ClrTypeName = "float"
                    },
                    new GameMemberNode
                    {
                        Kind = GameMemberKind.Parameter,
                        Behavior = MemberBehaviorBucket.Data,
                        Name = "gearDeployed",
                        Signature = "bool gearDeployed",
                        BindingId = "Member.Aircraft.gearDeployed",
                        ClrTypeName = "bool"
                    },
                    new GameMemberNode
                    {
                        Kind = GameMemberKind.Parameter,
                        Behavior = MemberBehaviorBucket.Data,
                        Name = "radarAlt",
                        Signature = "float radarAlt",
                        BindingId = "Member.Aircraft.radarAlt",
                        ClrTypeName = "float"
                    }
                ],
                Methods =
                [
                    new GameMemberNode
                    {
                        Kind = GameMemberKind.Method,
                        Behavior = MemberBehaviorBucket.Action,
                        Name = "SetGear",
                        Signature = "void SetGear(bool deployed)",
                        BindingId = "Member.Aircraft.SetGear",
                        ClrTypeName = "void"
                    },
                    new GameMemberNode
                    {
                        Kind = GameMemberKind.Method,
                        Behavior = MemberBehaviorBucket.Data,
                        Name = "IsLanded",
                        Signature = "bool IsLanded()",
                        BindingId = "Member.Aircraft.IsLanded",
                        ClrTypeName = "bool"
                    }
                ],
                Values = []
            }
        ]);
    }
}
