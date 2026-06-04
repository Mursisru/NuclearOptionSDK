using System;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Bridge;

public static class LiveTraceService
{
    private const int MaxEvents = 512;
    private const double LiveBroadcastIntervalSeconds = 0.5;

    private static readonly object Sync = new();
    private static readonly Queue<TraceEvent> Events = new();
    private static bool _active;
    private static DateTime _stopAtUtc;
    private static DateTime _lastBroadcastUtc = DateTime.MinValue;
    private static int _lastBroadcastCount;

    public static bool IsActive
    {
        get
        {
            lock (Sync)
            {
                return _active;
            }
        }
    }

    public static void Start(int windowMs)
    {
        MethodHunterService.EnsureInitialized(MethodHunterService.DefaultHarmonyId);

        lock (Sync)
        {
            Events.Clear();
            _active = true;
            _lastBroadcastUtc = DateTime.MinValue;
            _lastBroadcastCount = 0;
            if (windowMs <= 0)
            {
                _stopAtUtc = DateTime.MaxValue;
            }
            else
            {
                var bounded = windowMs < 50 ? 50 : (windowMs > 30_000 ? 30_000 : windowMs);
                _stopAtUtc = DateTime.UtcNow.AddMilliseconds(bounded);
            }
        }

        MethodHunterService.Arm();
    }

    public static TraceEventsPayload Stop()
    {
        MethodHunterService.Disarm();

        lock (Sync)
        {
            _active = false;
            return SnapshotUnsafe();
        }
    }

    public static TraceEventsPayload Snapshot()
    {
        lock (Sync)
        {
            return SnapshotUnsafe();
        }
    }

    public static void Record(string category, string typeName, string methodName, string details)
    {
        lock (Sync)
        {
            if (!_active)
            {
                return;
            }

            Events.Enqueue(new TraceEvent
            {
                timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                category = category,
                typeName = typeName,
                methodName = methodName,
                details = details
            });

            while (Events.Count > MaxEvents)
            {
                Events.Dequeue();
            }
        }
    }

    public static void Tick(WebSocketHost? host)
    {
        TraceEventsPayload? payload = null;
        var shouldBroadcastLive = false;

        lock (Sync)
        {
            if (_active && _stopAtUtc != DateTime.MaxValue && DateTime.UtcNow >= _stopAtUtc)
            {
                MethodHunterService.Disarm();
                _active = false;
                payload = SnapshotUnsafe();
            }
            else if (_active && _stopAtUtc == DateTime.MaxValue && host != null)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastBroadcastUtc).TotalSeconds >= LiveBroadcastIntervalSeconds)
                {
                    _lastBroadcastUtc = now;
                    _lastBroadcastCount = Events.Count;
                    shouldBroadcastLive = true;
                }
            }
        }

        if (payload != null)
        {
            host?.Broadcast(ProtocolJson.Create(MessageTypes.TraceEvents, payload));
        }

        if (shouldBroadcastLive)
        {
            host?.Broadcast(ProtocolJson.Create(MessageTypes.TraceEvents, Snapshot()));
        }
    }

    private static TraceEventsPayload SnapshotUnsafe() =>
        new()
        {
            tracingActive = _active,
            events = Events.ToArray()
        };
}
