namespace NuclearOptionSDK.Protocol;

public sealed class OverlayLabel
{
    public string id { get; set; } = string.Empty;
    public string text { get; set; } = "Label";
    public float x { get; set; }
    public float y { get; set; }
    public float fontSize { get; set; } = 18f;
    public string colorHtml { get; set; } = "#FFFFFF";
    public bool visible { get; set; } = true;
}

public sealed class VisualHudLayoutPayload
{
    public string name { get; set; } = "layout";
    public OverlayLabel[] labels { get; set; } = Array.Empty<OverlayLabel>();
    public OverlayPrimitive[] primitives { get; set; } = Array.Empty<OverlayPrimitive>();
}
