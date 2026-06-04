namespace NuclearOptionSDK.Studio.Services;

public sealed class AppSettings
{
    public string NuclearOptionRoot { get; set; } =
        @"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option";

    public int BridgePort { get; set; } = 9005;
    public bool ReplDisclaimerAccepted { get; set; }
}
