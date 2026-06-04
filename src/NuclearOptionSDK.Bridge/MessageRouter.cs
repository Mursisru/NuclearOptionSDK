using System;
using System.Threading.Tasks;
using Fleck;
using HarmonyLib;
using NuclearOptionSDK.ModKit;
using NuclearOptionSDK.Protocol;
using UnityEngine;

namespace NuclearOptionSDK.Bridge;

public sealed class MessageRouter
{
    private readonly MainThreadDispatcher _dispatcher;
    private readonly RoslynScriptHost _scriptHost;
    private readonly WebSocketHost _webSocketHost;

    public MessageRouter(MainThreadDispatcher dispatcher, RoslynScriptHost scriptHost, WebSocketHost webSocketHost)
    {
        _dispatcher = dispatcher;
        _scriptHost = scriptHost;
        _webSocketHost = webSocketHost;
    }

    public void HandleIncoming(IWebSocketConnection socket, string json)
    {
        MessageEnvelope envelope;
        try
        {
            envelope = ProtocolJson.Deserialize(json);
        }
        catch (Exception ex)
        {
            _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.Error, new ErrorPayload { message = ex.Message }));
            return;
        }

        BridgeFileLogger.Info("ipc.recv", $"{envelope.type} id={envelope.id}");

        if (envelope.type == MessageTypes.Ping)
        {
            _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.Pong, new PongPayload
            {
                bridgeVersion = AppVersion.Display,
                gameVersion = Application.unityVersion
            }, envelope.id));
            return;
        }

        if (envelope.type == MessageTypes.HarmonyGenerate)
        {
            var request = ProtocolJson.Payload<HarmonyGenerateRequest>(envelope);
            var source = HarmonyPatchGenerator.Generate(request);
            _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.HarmonyGenerated, new HarmonyGenerateResponse
            {
                sourceCode = source
            }, envelope.id));
            return;
        }

        if (envelope.type == MessageTypes.ModBuild)
        {
            HandleModBuild(socket, envelope);
            return;
        }

        if (envelope.type == MessageTypes.TraceStart)
        {
            var request = ProtocolJson.Payload<TraceStartRequest>(envelope);
            var responseId = envelope.id;
            // Harmony patches must run on Unity main thread (Fleck WS thread would silently fail).
            _dispatcher.Enqueue(() => HandleTraceStart(socket, request, responseId));
            return;
        }

        if (envelope.type == MessageTypes.TraceStop)
        {
            var responseId = envelope.id;
            _dispatcher.Enqueue(() => HandleTraceStop(socket, responseId));
            return;
        }

        _dispatcher.Enqueue(() => HandleOnMainThread(socket, envelope));
    }

    private void HandleOnMainThread(IWebSocketConnection socket, MessageEnvelope envelope)
    {
        try
        {
            switch (envelope.type)
            {
                case MessageTypes.SceneGetRoots:
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.SceneTree, SceneScanner.CaptureActiveScene(), envelope.id));
                    break;

                case MessageTypes.SceneResolve:
                    var resolve = ProtocolJson.Payload<SceneResolveRequest>(envelope);
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.SceneResolved, SceneScanner.Resolve(resolve.instanceId), envelope.id));
                    break;

                case MessageTypes.ExecuteCode:
                    HandleExecuteCode(socket, envelope);
                    break;

                case MessageTypes.HudGetTree:
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.HudTree, FlightHudService.CaptureHudTree(), envelope.id));
                    break;

                case MessageTypes.HudUpdate:
                    var hudUpdate = ProtocolJson.Payload<HudUpdateRequest>(envelope);
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.HudUpdated, FlightHudService.ApplyUpdate(hudUpdate), envelope.id));
                    break;

                case MessageTypes.OverlaySetEnabled:
                    var overlayEnabled = ProtocolJson.Payload<OverlayEnabledRequest>(envelope);
                    BridgeRuntime.Overlay?.SetEnabled(overlayEnabled.enabled);
                    break;

                case MessageTypes.OverlayDraw:
                    var overlayDraw = ProtocolJson.Payload<OverlayDrawRequest>(envelope);
                    BridgeRuntime.Overlay?.SetDraw(overlayDraw);
                    break;

                case MessageTypes.OverlayLayout:
                    var overlayLayout = ProtocolJson.Payload<VisualHudLayoutPayload>(envelope);
                    BridgeRuntime.Overlay?.SetLayout(overlayLayout);
                    break;

                case MessageTypes.LogicSet:
                    var logicSet = ProtocolJson.Payload<LogicSetRequest>(envelope);
                    BridgeRuntime.Logic?.SetProject(logicSet.project, logicSet.previewEnabled);
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.LogicStatus, BridgeRuntime.Logic?.Status() ?? new LogicStatusPayload(), envelope.id));
                    break;

                case MessageTypes.LogicPreview:
                    var logicPreview = ProtocolJson.Payload<LogicPreviewRequest>(envelope);
                    BridgeRuntime.Logic?.SetPreview(logicPreview.enabled);
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.LogicStatus, BridgeRuntime.Logic?.Status() ?? new LogicStatusPayload(), envelope.id));
                    break;

                case MessageTypes.BindingList:
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.BindingList, BridgeRuntime.Bindings?.ListBindings() ?? new BindingListPayload(), envelope.id));
                    break;

                case MessageTypes.BindingResolve:
                    var bindReq = ProtocolJson.Payload<BindingResolveRequest>(envelope);
                    BridgeRuntime.Telemetry?.Refresh();
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.BindingResolved, BridgeRuntime.Bindings?.Resolve(bindReq) ?? new BindingResolveResponse { success = false, error = "Bindings unavailable" }, envelope.id));
                    break;

                case MessageTypes.ReferenceList:
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.ReferenceList, ReferenceCatalog.List(), envelope.id));
                    break;

                case MessageTypes.DependencyRadar:
                    var depReq = ProtocolJson.Payload<DependencyRadarRequest>(envelope);
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.DependencyRadarResult, new DependencyRadarPayload
                    {
                        bindingId = depReq.bindingId,
                        readers = Array.Empty<DependencyNode>(),
                        writers = Array.Empty<DependencyNode>(),
                        warnings = new[] { "Bridge runtime radar is unavailable; use Studio index radar." }
                    }, envelope.id));
                    break;

                default:
                    _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.Error, new ErrorPayload
                    {
                        message = $"Unknown message type: {envelope.type}"
                    }, envelope.id));
                    break;
            }
        }
        catch (Exception ex)
        {
            BridgeFileLogger.Error("ipc.recv", ex.ToString());
            _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.Error, new ErrorPayload { message = ex.ToString() }, envelope.id));
        }
    }

    private void HandleExecuteCode(IWebSocketConnection socket, MessageEnvelope envelope)
    {
        var request = ProtocolJson.Payload<ExecuteCodeRequest>(envelope);
        var response = _scriptHost.ExecuteAsync(request.code).GetAwaiter().GetResult();
        _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.ExecuteResult, response, envelope.id));
    }

    private void HandleTraceStart(IWebSocketConnection socket, TraceStartRequest request, string responseId)
    {
        try
        {
            LiveTraceService.Start(request.windowMs);
            _webSocketHost.Send(
                socket,
                ProtocolJson.Create(MessageTypes.TraceEvents, LiveTraceService.Snapshot(), responseId));
        }
        catch (Exception ex)
        {
            BridgeFileLogger.Error("trace.start", ex.ToString());
            _webSocketHost.Send(
                socket,
                ProtocolJson.Create(MessageTypes.Error, new ErrorPayload { message = ex.ToString() }, responseId));
        }
    }

    private void HandleTraceStop(IWebSocketConnection socket, string responseId)
    {
        try
        {
            var payload = LiveTraceService.Stop();
            _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.TraceEvents, payload, responseId));
        }
        catch (Exception ex)
        {
            BridgeFileLogger.Error("trace.stop", ex.ToString());
            _webSocketHost.Send(
                socket,
                ProtocolJson.Create(MessageTypes.Error, new ErrorPayload { message = ex.ToString() }, responseId));
        }
    }

    private void HandleModBuild(IWebSocketConnection socket, MessageEnvelope envelope)
    {
        var request = ProtocolJson.Payload<ModBuildRequest>(envelope);
        Task.Run(() =>
        {
            var response = ModProjectBuilder.Build(request, BridgeRuntime.NuclearOptionRoot);
            _webSocketHost.Send(socket, ProtocolJson.Create(MessageTypes.ModBuilt, response, envelope.id));
        });
    }
}
