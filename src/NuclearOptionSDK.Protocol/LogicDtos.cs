namespace NuclearOptionSDK.Protocol;

public sealed class LogicProject
{
    public string name { get; set; } = "logic-project";
    public string version { get; set; } = "1";
    public double tickRateHz { get; set; } = 10;
    public string mergeMode { get; set; } = "all";
    public string executionMode { get; set; } = "Update";
    public bool diagnosticMode { get; set; }
    public string? patchTargetType { get; set; }
    public string? patchMethodName { get; set; }
    public string patchKind { get; set; } = "Prefix";
    public string? referenceId { get; set; }
    public LogicGraph referenceGraph { get; set; } = new();
    public LogicGraph userGraph { get; set; } = new();
    public LogicUILayout? layout { get; set; }
}

public sealed class LogicGraph
{
    public LogicNode[] nodes { get; set; } = Array.Empty<LogicNode>();
    public LogicEdge[] edges { get; set; } = Array.Empty<LogicEdge>();
}

public sealed class LogicNode
{
    public string id { get; set; } = string.Empty;
    public string kind { get; set; } = "source";
    public string typeId { get; set; } = string.Empty;
    public float x { get; set; }
    public float y { get; set; }
    public Dictionary<string, string> parameters { get; set; } = new();
}

public sealed class LogicEdge
{
    public string fromNode { get; set; } = string.Empty;
    public string fromPort { get; set; } = "out";
    public string toNode { get; set; } = string.Empty;
    public string toPort { get; set; } = "in";
    public Dictionary<string, string> parameters { get; set; } = new();
}

public sealed class LogicUILayout
{
    public double splitRatio { get; set; } = 0.5;
    public double workspaceTopRowWeight { get; set; } = 5;
    public double workspaceBottomRowWeight { get; set; } = 2;
    public LogicZone[] zones { get; set; } = Array.Empty<LogicZone>();
}

public sealed class LogicZone
{
    public string id { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string kind { get; set; } = "UserGraph";
    public double width { get; set; } = 1;
    public string dock { get; set; } = "center";
    public bool visible { get; set; } = true;
}

public sealed class BindingDescriptor
{
    public string id { get; set; } = string.Empty;
    public string kind { get; set; } = "telemetry";
    public string path { get; set; } = string.Empty;
    public string valueType { get; set; } = "float";
}

public sealed class DisplayEntry
{
    public string id { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string hint { get; set; } = string.Empty;
    public string category { get; set; } = string.Empty;
}

public sealed class ReferenceGraphPayload
{
    public string id { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string? gameVersionMin { get; set; }
    public LogicGraph graph { get; set; } = new();
}

public sealed class ReferenceListPayload
{
    public ReferenceGraphPayload[] references { get; set; } = Array.Empty<ReferenceGraphPayload>();
}

public sealed class LogicSetRequest
{
    public LogicProject project { get; set; } = new();
    public bool previewEnabled { get; set; } = true;
}

public sealed class LogicStatusPayload
{
    public bool previewEnabled { get; set; }
    public bool running { get; set; }
    public string? lastError { get; set; }
    public LogicActionResult[] lastActions { get; set; } = Array.Empty<LogicActionResult>();
}

public sealed class LogicPreviewRequest
{
    public bool enabled { get; set; } = true;
}

public sealed class BindingListPayload
{
    public BindingDescriptor[] bindings { get; set; } = Array.Empty<BindingDescriptor>();
}

public sealed class BindingResolveRequest
{
    public string bindingId { get; set; } = string.Empty;
}

public sealed class BindingResolveResponse
{
    public bool success { get; set; }
    public string? error { get; set; }
    public string bindingId { get; set; } = string.Empty;
    public double? floatValue { get; set; }
    public bool? boolValue { get; set; }
    public string? stringValue { get; set; }
}

public sealed class BindingWatchPayload
{
    public Dictionary<string, double> telemetry { get; set; } = new();
    public Dictionary<string, bool> flags { get; set; } = new();
}

public sealed class LogicActionResult
{
    public string typeId { get; set; } = string.Empty;
    public string? labelId { get; set; }
    public string? colorHtml { get; set; }
    public bool? visible { get; set; }
    public int? hudInstanceId { get; set; }
    public string? text { get; set; }
}

public sealed class TraceStartRequest
{
    public int windowMs { get; set; } = 300;
}

public sealed class TraceEvent
{
    public long timestampUnixMs { get; set; }
    public string category { get; set; } = string.Empty;
    public string typeName { get; set; } = string.Empty;
    public string methodName { get; set; } = string.Empty;
    public string details { get; set; } = string.Empty;
}

public sealed class TraceEventsPayload
{
    public bool tracingActive { get; set; }
    public TraceEvent[] events { get; set; } = Array.Empty<TraceEvent>();
}

public sealed class DependencyRadarRequest
{
    public string bindingId { get; set; } = string.Empty;
}

public sealed class DependencyNode
{
    public string typeName { get; set; } = string.Empty;
    public string methodName { get; set; } = string.Empty;
    public string usage { get; set; } = string.Empty;
}

public sealed class DependencyRadarPayload
{
    public string bindingId { get; set; } = string.Empty;
    public DependencyNode[] writers { get; set; } = Array.Empty<DependencyNode>();
    public DependencyNode[] readers { get; set; } = Array.Empty<DependencyNode>();
    public string[] warnings { get; set; } = Array.Empty<string>();
}

public sealed class NosdkProject
{
    public string name { get; set; } = "project";
    public VisualHudLayoutPayload? visualHud { get; set; }
    public LogicProject? logic { get; set; }
}
