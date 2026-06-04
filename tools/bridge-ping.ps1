param(
    [int]$Port = 9005,
    [int]$Attempts = 60,
    [int]$DelaySeconds = 2
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.WebSockets
Add-Type -AssemblyName System.Net.Http

function Test-TcpPort {
    param([int]$Port)
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $client.Connect("127.0.0.1", $Port)
        $client.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Send-WsPing {
    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $cts = New-Object System.Threading.CancellationTokenSource
    $cts.CancelAfter(5000)
    $uri = [Uri]"ws://127.0.0.1:$Port"
    $ws.ConnectAsync($uri, $cts.Token).GetAwaiter().GetResult() | Out-Null
    $id = [Guid]::NewGuid().ToString("N")
    $payload = "{""id"":""$id"",""type"":""ping"",""payload"":null}"
    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $segment = [ArraySegment[byte]]::new($bytes)
    $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).GetAwaiter().GetResult() | Out-Null
    $buffer = New-Object byte[] 8192
    $recv = $ws.ReceiveAsync([ArraySegment[byte]]::new($buffer), $cts.Token).GetAwaiter().GetResult()
    $text = [Text.Encoding]::UTF8.GetString($buffer, 0, $recv.Count)
    $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "bye", $cts.Token).GetAwaiter().GetResult() | Out-Null
    return $text
}

Write-Host "Waiting for Bridge on ws://127.0.0.1:$Port ($Attempts attempts)..."

for ($i = 1; $i -le $Attempts; $i++) {
    if (Test-TcpPort -Port $Port) {
        Write-Host "[$i] Port $Port is open. Sending ping..."
        try {
            $response = Send-WsPing
            Write-Host "SUCCESS: $response"
            exit 0
        }
        catch {
            Write-Host "[$i] Port open but WebSocket failed: $($_.Exception.Message)"
        }
    }
    else {
        Write-Host "[$i] Port closed..."
    }

    Start-Sleep -Seconds $DelaySeconds
}

Write-Host "FAILED: Bridge not reachable."
exit 1
