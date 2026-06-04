using System;
using System.Collections.Concurrent;
using BepInEx.Logging;

namespace NuclearOptionSDK.Bridge;

public sealed class MainThreadDispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();

    public void Enqueue(Action action)
    {
        _queue.Enqueue(action);
    }

    public void Tick()
    {
        while (_queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                BridgeRuntime.Log?.LogError($"MainThreadDispatcher: {ex}");
            }
        }
    }
}
