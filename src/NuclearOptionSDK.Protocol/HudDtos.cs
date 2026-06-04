using System.Collections.Generic;



namespace NuclearOptionSDK.Protocol;



public sealed class HudElementNode

{

    public string path { get; set; } = string.Empty;

    public int instanceId { get; set; }

    public string type { get; set; } = string.Empty;

    public string text { get; set; } = string.Empty;

    public bool active { get; set; } = true;

    public List<HudElementNode> children { get; set; } = new();

}



public sealed class HudTreePayload

{

    public bool found { get; set; }

    public List<HudElementNode> elements { get; set; } = new();

}



public sealed class HudUpdateRequest

{

    public int instanceId { get; set; }

    public bool? active { get; set; }

    public string? colorHtml { get; set; }

    public float? fontSize { get; set; }

    public string? text { get; set; }

}



public sealed class HudUpdateResponse

{

    public bool success { get; set; }

    public string? error { get; set; }

}



public sealed class OverlayEnabledRequest

{

    public bool enabled { get; set; }

}



public sealed class OverlayDrawRequest

{

    public bool clear { get; set; }

    public OverlayPrimitive[] primitives { get; set; } = Array.Empty<OverlayPrimitive>();

    public OverlayLabel[] labels { get; set; } = Array.Empty<OverlayLabel>();

}



public sealed class OverlayPrimitive

{

    public string kind { get; set; } = "line";

    public float x1 { get; set; }

    public float y1 { get; set; }

    public float x2 { get; set; }

    public float y2 { get; set; }

    public float radius { get; set; }

    public string colorHtml { get; set; } = "#FF0000";

}

