using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using NuclearOptionSDK.Protocol;

const int port = 9005;
const int attempts = 45;
const int delayMs = 2000;

Console.WriteLine($"Waiting for ws://127.0.0.1:{port} ...");

for (var i = 1; i <= attempts; i++)
{
    if (!IsPortOpen(port))
    {
        Console.WriteLine($"[{i}] port closed");
        await Task.Delay(delayMs);
        continue;
    }

    Console.WriteLine($"[{i}] port open, sending ping...");
    try
    {
        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), cts.Token);
        var envelope = ProtocolJson.Create(MessageTypes.Ping);
        var json = ProtocolJson.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token);

        var buffer = new byte[8192];
        var result = await ws.ReceiveAsync(buffer, cts.Token);
        var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Console.WriteLine($"SUCCESS: {response}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{i}] websocket error: {ex.Message}");
    }

    await Task.Delay(delayMs);
}

Console.WriteLine("FAILED: Bridge not reachable. Restart NO after deploy.");
return 1;

static bool IsPortOpen(int port)
{
    try
    {
        using var client = new TcpClient();
        client.Connect("127.0.0.1", port);
        return true;
    }
    catch
    {
        return false;
    }
}
