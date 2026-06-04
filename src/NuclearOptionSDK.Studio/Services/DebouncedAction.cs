namespace NuclearOptionSDK.Studio.Services;

public sealed class DebouncedAction : IDisposable
{
    private readonly Action _action;
    private readonly int _delayMs;
    private CancellationTokenSource? _cts;

    public DebouncedAction(Action action, int delayMs = 280)
    {
        _action = action;
        _delayMs = delayMs;
    }

    public void Trigger()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = RunDelayedAsync(token);
    }

    private async Task RunDelayedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_delayMs, token);
            if (!token.IsCancellationRequested)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(_action);
            }
        }
        catch (TaskCanceledException)
        {
            // debounce cancelled
        }
    }

    public void Dispose() => _cts?.Cancel();
}
