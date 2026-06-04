namespace NuclearOptionSDK.Protocol;

public sealed class MessageEnvelope
{
    public int v { get; set; } = ProtocolVersion.Current;
    public string type { get; set; } = string.Empty;
    public string id { get; set; } = string.Empty;
    public object? payload { get; set; }
}
