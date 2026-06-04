using System;
using System.Collections.Generic;
using Fleck;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Bridge;

public sealed class WebSocketHost : IDisposable
{
    private MessageRouter? _router;
    private WebSocketServer? _server;
    private readonly HashSet<IWebSocketConnection> _clients = new();

    public void Bind(MessageRouter router)
    {
        _router = router;
    }

    public void Start(int port)
    {
        Stop();
        _server = new WebSocketServer($"ws://127.0.0.1:{port}");
        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                lock (_clients)
                {
                    _clients.Add(socket);
                }
            };

            socket.OnClose = () =>
            {
                lock (_clients)
                {
                    _clients.Remove(socket);
                }
            };

            socket.OnMessage = message =>
            {
                if (_router == null)
                {
                    return;
                }

                _router.HandleIncoming(socket, message);
            };
        });

        BridgeRuntime.Log?.LogInfo($"WebSocket server listening on ws://127.0.0.1:{port}");
    }

    public void Stop()
    {
        lock (_clients)
        {
            foreach (var client in _clients)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    // ignored
                }
            }

            _clients.Clear();
        }

        _server?.Dispose();
        _server = null;
    }

    public void Broadcast(MessageEnvelope envelope)
    {
        var json = ProtocolJson.Serialize(envelope);
        lock (_clients)
        {
            foreach (var client in _clients)
            {
                try
                {
                    client.Send(json);
                }
                catch (Exception ex)
                {
                    BridgeRuntime.Log?.LogWarning($"WS send failed: {ex.Message}");
                }
            }
        }
    }

    public void Send(IWebSocketConnection socket, MessageEnvelope envelope)
    {
        socket.Send(ProtocolJson.Serialize(envelope));
    }

    public void Dispose()
    {
        Stop();
    }
}
