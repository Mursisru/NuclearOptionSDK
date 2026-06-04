using System;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Bridge;

public static class ReferenceCatalog
{
    public static ReferenceListPayload List()
    {
        return new ReferenceListPayload
        {
            references = new[]
            {
                BuildAoAReference(),
                BuildSpeedAltGReference(),
                BuildFuelReference(),
                BuildWeaponReference()
            }
        };
    }

    private static ReferenceGraphPayload BuildAoAReference()
    {
        return new ReferenceGraphPayload
        {
            id = "aoa-display",
            title = "AoADisplay (игровой HUD)",
            graph = new LogicGraph
            {
                nodes = new[]
                {
                    new LogicNode { id = "n1", kind = "source", typeId = "Telemetry.AoA", x = 40, y = 80 },
                    new LogicNode { id = "n4", kind = "check", typeId = "Gate.OnlyWhenInFlight", x = 240, y = 80, parameters = new Dictionary<string, string> { ["threshold"] = "10" } },
                    new LogicNode { id = "n5", kind = "check", typeId = "Compare.GreaterThan", x = 440, y = 80, parameters = new Dictionary<string, string> { ["threshold"] = "15" } },
                    new LogicNode { id = "n6", kind = "output", typeId = "Action.SetOverlayColor", x = 640, y = 80, parameters = new Dictionary<string, string> { ["labelId"] = "aoa-label", ["colorHtml"] = "#FF4400" } }
                },
                edges = new[]
                {
                    new LogicEdge { fromNode = "n1", toNode = "n4" },
                    new LogicEdge { fromNode = "n4", toNode = "n5" },
                    new LogicEdge { fromNode = "n5", toNode = "n6" }
                }
            }
        };
    }

    private static ReferenceGraphPayload BuildSpeedAltGReference()
    {
        return new ReferenceGraphPayload
        {
            id = "speed-alt-g",
            title = "Speed / Altitude / G",
            graph = new LogicGraph
            {
                nodes = new[]
                {
                    new LogicNode { id = "s1", kind = "source", typeId = "Telemetry.Speed", x = 40, y = 60 },
                    new LogicNode { id = "s2", kind = "source", typeId = "Telemetry.Altitude", x = 40, y = 140 },
                    new LogicNode { id = "s3", kind = "source", typeId = "Telemetry.G", x = 40, y = 220 }
                },
                edges = Array.Empty<LogicEdge>()
            }
        };
    }

    private static ReferenceGraphPayload BuildFuelReference()
    {
        return new ReferenceGraphPayload
        {
            id = "fuel-status",
            title = "Fuel status",
            graph = new LogicGraph
            {
                nodes = new[]
                {
                    new LogicNode { id = "f1", kind = "source", typeId = "Telemetry.Fuel", x = 40, y = 80 },
                    new LogicNode { id = "f2", kind = "check", typeId = "Gate.FuelLow", x = 240, y = 80, parameters = new Dictionary<string, string> { ["threshold"] = "0.15" } },
                    new LogicNode { id = "f3", kind = "output", typeId = "Action.SetOverlayVisible", x = 440, y = 80, parameters = new Dictionary<string, string> { ["labelId"] = "fuel-warn", ["visible"] = "true" } }
                },
                edges = new[]
                {
                    new LogicEdge { fromNode = "f1", toNode = "f2" },
                    new LogicEdge { fromNode = "f2", toNode = "f3" }
                }
            }
        };
    }

    private static ReferenceGraphPayload BuildWeaponReference()
    {
        return new ReferenceGraphPayload
        {
            id = "weapon-status",
            title = "Weapon status",
            graph = new LogicGraph
            {
                nodes = new[]
                {
                    new LogicNode { id = "w1", kind = "gate", typeId = "Gate.WeaponSelected", x = 40, y = 80 },
                    new LogicNode { id = "w2", kind = "output", typeId = "Action.SetHudActive", x = 240, y = 80, parameters = new Dictionary<string, string> { ["active"] = "true" } }
                },
                edges = new[]
                {
                    new LogicEdge { fromNode = "w1", toNode = "w2" }
                }
            }
        };
    }
}
