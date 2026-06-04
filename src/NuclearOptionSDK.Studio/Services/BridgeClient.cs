using System.Net.WebSockets;
using System.Text;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public sealed class BridgeClient : IAsyncDisposable
{
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly Dictionary<string, TaskCompletionSource<MessageEnvelope>> _pending = new();
    private readonly object _sync = new();

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public event Action<MessageEnvelope>? MessageReceived;
    public event Action<string>? ConnectionStateChanged;
    public event Action<string>? Log;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync().ConfigureAwait(false);

        _socket = new ClientWebSocket();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var uri = new Uri($"ws://{host}:{port}");
        await _socket.ConnectAsync(uri, _cts.Token).ConfigureAwait(false);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        ConnectionStateChanged?.Invoke("Connected");
        StudioFileLogger.Info("connect", uri.ToString());
        Log?.Invoke($"Connected to {uri}");
    }

    public async Task DisconnectAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        if (_socket != null)
        {
            if (_socket.State == WebSocketState.Open)
            {
                try
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }

            _socket.Dispose();
            _socket = null;
        }

        _cts?.Dispose();
        _cts = null;
        ConnectionStateChanged?.Invoke("Disconnected");
    }

    public async Task<MessageEnvelope?> SendAsync(MessageEnvelope envelope, TimeSpan timeout)
    {
        if (_socket == null || _socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Not connected.");
        }

        var tcs = new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync)
        {
            _pending[envelope.id] = tcs;
        }

        var json = ProtocolJson.Serialize(envelope);
        StudioFileLogger.Info("ipc.send", $"{envelope.type} id={envelope.id} bytes={json.Length}");
        var bytes = Encoding.UTF8.GetBytes(json);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);

        using var cts = new CancellationTokenSource(timeout);
        await using var reg = cts.Token.Register(() => tcs.TrySetCanceled());
        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _pending.Remove(envelope.id);
            }
        }
    }

    public Task SendFireAndForgetAsync(MessageEnvelope envelope)
    {
        if (_socket == null || _socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Not connected.");
        }

        var json = ProtocolJson.Serialize(envelope);
        StudioFileLogger.Info("ipc.send.fire", $"{envelope.type} id={envelope.id}");
        var bytes = Encoding.UTF8.GetBytes(json);
        return _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var builder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested && _socket?.State == WebSocketState.Open)
        {
            builder.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    ConnectionStateChanged?.Invoke("Disconnected");
                    return;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            MessageEnvelope envelope;
            try
            {
                envelope = ProtocolJson.Deserialize(builder.ToString());
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Bad message: {ex.Message}");
                continue;
            }

            TaskCompletionSource<MessageEnvelope>? tcs = null;
            lock (_sync)
            {
                _pending.TryGetValue(envelope.id, out tcs);
            }

            if (tcs != null)
            {
                tcs.TrySetResult(envelope);
            }
            else
            {
                StudioFileLogger.Info("ipc.recv", $"{envelope.type} id={envelope.id}");
                MessageReceived?.Invoke(envelope);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }
}
