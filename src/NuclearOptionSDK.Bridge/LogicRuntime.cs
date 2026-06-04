using System;
using NuclearOptionSDK.LogicCore;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Bridge;

public sealed class LogicRuntime
{
    private readonly TelemetryService _telemetry;
    private readonly LogicGraphEvaluator _evaluator = new();
    private readonly LogicStateStore _state = new();
    private LogicProject _project = new();
    private bool _previewEnabled;
    private float _tickTimer;
    private float _watchTimer;
    private string? _lastError;
    private LogicActionResult[] _lastActions = Array.Empty<LogicActionResult>();

    public LogicRuntime(TelemetryService telemetry)
    {
        _telemetry = telemetry;
    }

    public bool PreviewEnabled => _previewEnabled;
    public LogicActionResult[] LastActions => _lastActions;
    public string? LastError => _lastError;

    public void SetProject(LogicProject project, bool previewEnabled)
    {
        _project = project ?? new LogicProject();
        _previewEnabled = previewEnabled;
        _state.Reset();
        _lastError = null;
        BridgeFileLogger.Info("logic.set", $"name={_project.name} preview={previewEnabled}");
    }

    public void SetPreview(bool enabled)
    {
        _previewEnabled = enabled;
        BridgeFileLogger.Info("logic.preview", $"enabled={enabled}");
    }

    public LogicStatusPayload Status() => new()
    {
        previewEnabled = _previewEnabled,
        running = _previewEnabled,
        lastError = _lastError,
        lastActions = _lastActions
    };

    public void Tick(float deltaTime, WebSocketHost? host)
    {
        if (!_previewEnabled)
        {
            return;
        }

        _state.Tick(deltaTime);
        var tickHz = (float)Math.Max(1, _project.tickRateHz);
        _tickTimer += deltaTime;
        if (_tickTimer < 1f / tickHz)
        {
            return;
        }

        _tickTimer = 0f;
        _telemetry.Refresh();

        try
        {
            var result = _evaluator.Evaluate(_project, _telemetry, _state);
            _lastActions = result.Actions;
            ApplyActions(result.Actions);
            if (_project.diagnosticMode)
            {
                LiveTraceService.Record(
                    "logic.tick",
                    "LogicGraphEvaluator",
                    "Evaluate",
                    $"actions={result.Actions.Length} tickHz={_project.tickRateHz:0.##}");
            }
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            BridgeFileLogger.Error("logic.tick", ex.ToString());
        }

        _watchTimer += deltaTime;
        if (host != null && _watchTimer >= 0.5f)
        {
            _watchTimer = 0f;
            host.Broadcast(ProtocolJson.Create(MessageTypes.BindingWatch, _telemetry.Snapshot()));
        }
    }

    private void ApplyActions(LogicActionResult[] actions)
    {
        foreach (var action in actions)
        {
            switch (action.typeId)
            {
                case "Action.SetOverlayColor":
                case "Action.SetOverlayVisible":
                    BridgeRuntime.Overlay?.ApplyLogicAction(action);
                    break;

                case "Action.SetHudColor":
                case "Action.SetHudText":
                case "Action.SetHudActive":
                    ApplyHudAction(action);
                    break;

                case "Action.PlaySound":
                    ApplySound(action);
                    break;

                case "Action.SetMemberBind":
                    MemberWriteService.TryApply(action);
                    break;

                case "Action.CatalogWrite":
                    if (action.text?.StartsWith("Member.", StringComparison.Ordinal) == true
                        || action.text?.StartsWith("Write.", StringComparison.Ordinal) == true)
                    {
                        var bind = action.text!.StartsWith("Write.", StringComparison.Ordinal)
                            ? "Member." + action.text.Substring("Write.".Length)
                            : action.text;
                        MemberWriteService.TryWrite(bind, action.colorHtml ?? string.Empty);
                    }

                    break;
            }
        }
    }

    private static void ApplyHudAction(LogicActionResult action)
    {
        if (action.hudInstanceId == null || action.hudInstanceId == 0)
        {
            return;
        }

        FlightHudService.ApplyUpdate(new HudUpdateRequest
        {
            instanceId = action.hudInstanceId.Value,
            active = action.visible,
            colorHtml = action.colorHtml,
            text = action.text
        });
    }

    private static void ApplySound(LogicActionResult action)
    {
        if (string.IsNullOrEmpty(action.text))
        {
            return;
        }

        BridgeFileLogger.Info("logic.sound", action.text!);
    }
}
