using Avalonia.Controls;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Views;

public partial class ProtocolLivePanel : UserControl
{
    public event Func<Task>? TraceStartRequested;
    public event Func<Task>? TraceStopRequested;

    public ProtocolLivePanel()
    {
        InitializeComponent();
        TraceStartButton.Click += async (_, _) =>
        {
            TraceStatusText.Text = "tracing...";
            if (TraceStartRequested != null)
            {
                await TraceStartRequested.Invoke();
            }
        };
        TraceStopButton.Click += async (_, _) =>
        {
            if (TraceStopRequested != null)
            {
                await TraceStopRequested.Invoke();
            }
        };
    }

    public void SetJsonPreview(string json) => JsonPreviewBox.Text = json;

    public void UpdateTelemetry(BindingWatchPayload payload)
    {
        if (payload.telemetry.Count == 0)
        {
            TelemetryBox.Text = string.Empty;
            return;
        }

        TelemetryBox.Text = string.Join(Environment.NewLine,
            payload.telemetry.Select(kv => $"{kv.Key} = {kv.Value:F3}"));
    }

    public void UpdateTrace(TraceEventsPayload payload)
    {
        var events = payload.events ?? Array.Empty<TraceEvent>();
        TraceStatusText.Text = payload.tracingActive
            ? $"tracing… ({events.Length} events)"
            : $"captured ({events.Length} events)";
        if (events.Length == 0)
        {
            JsonPreviewBox.Text = payload.tracingActive
                ? "// trace: waiting… (fly / act in game; events appear every ~0.5s)"
                : "// trace: no events — check BepInEx log for [MethodHunter] patched=…";
            return;
        }

        JsonPreviewBox.Text = string.Join(
            Environment.NewLine,
            events.Select(e =>
                $"{e.timestampUnixMs} | {e.category} | {e.typeName}.{e.methodName} | {e.details}"));
    }
}
