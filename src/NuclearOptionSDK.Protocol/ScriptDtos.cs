namespace NuclearOptionSDK.Protocol;

public sealed class ExecuteCodeRequest
{
    public string code { get; set; } = string.Empty;
}

public sealed class ExecuteCodeResponse
{
    public bool success { get; set; }
    public string? result { get; set; }
    public string? error { get; set; }
}

public sealed class ErrorPayload
{
    public string message { get; set; } = string.Empty;
}

public sealed class LogPayload
{
    public string level { get; set; } = "info";
    public string message { get; set; } = string.Empty;
}

public sealed class PongPayload
{
    public string bridgeVersion { get; set; } = string.Empty;
    public string gameVersion { get; set; } = string.Empty;
}
