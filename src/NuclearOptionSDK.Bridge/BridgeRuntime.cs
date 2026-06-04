using BepInEx.Logging;
using NuclearOptionSDK.Protocol;
using UnityEngine;

namespace NuclearOptionSDK.Bridge;

public static class BridgeRuntime
{
    public static ManualLogSource? Log { get; internal set; }
    public static bool IsRunning { get; internal set; }
    public static string NuclearOptionRoot { get; internal set; } = string.Empty;
    public static HudOverlayInjector? Overlay { get; internal set; }
    public static LogicRuntime? Logic { get; internal set; }
    public static TelemetryService? Telemetry { get; internal set; }
    public static BindingResolver? Bindings { get; internal set; }

    private static MainThreadDispatcher? _dispatcher;
    private static WebSocketHost? _webSocketHost;
    private static float _audioBroadcastTimer;

    public static void Initialize(
        ManualLogSource log,
        string nuclearOptionRoot,
        MainThreadDispatcher dispatcher,
        WebSocketHost webSocketHost,
        HudOverlayInjector overlay)
    {
        Log = log;
        NuclearOptionRoot = nuclearOptionRoot;
        _dispatcher = dispatcher;
        _webSocketHost = webSocketHost;
        Overlay = overlay;
        Telemetry = new TelemetryService();
        Bindings = new BindingResolver(Telemetry);
        Logic = new LogicRuntime(Telemetry);
        IsRunning = true;
    }

    public static void Shutdown()
    {
        IsRunning = false;
        _webSocketHost?.Dispose();
        _webSocketHost = null;
        Overlay = null;
        Logic = null;
        Telemetry = null;
        Bindings = null;
    }

    public static void Tick()
    {
        _dispatcher?.Tick();

        if (!IsRunning || _webSocketHost == null)
        {
            return;
        }

        Logic?.Tick(Time.unscaledDeltaTime, _webSocketHost);

        _audioBroadcastTimer += Time.unscaledDeltaTime;

        LiveTraceService.Tick(_webSocketHost);

        if (_audioBroadcastTimer < 0.25f)
        {
            return;
        }

        _audioBroadcastTimer = 0f;
        var events = AudioTrackerService.Drain();
        foreach (var audioEvent in events)
        {
            _webSocketHost.Broadcast(ProtocolJson.Create(MessageTypes.AudioEvent, audioEvent));
        }
    }
}
