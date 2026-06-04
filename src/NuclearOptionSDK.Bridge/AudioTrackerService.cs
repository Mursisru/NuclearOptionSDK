using System.Collections.Generic;
using NuclearOptionSDK.Protocol;
using UnityEngine;

namespace NuclearOptionSDK.Bridge;

public static class AudioTrackerService
{
    private static readonly Queue<AudioEventPayload> Pending = new();
    private static readonly object Sync = new();

    public static void Enqueue(AudioEventPayload payload)
    {
        lock (Sync)
        {
            Pending.Enqueue(payload);
            while (Pending.Count > 32)
            {
                Pending.Dequeue();
            }
        }
    }

    public static List<AudioEventPayload> Drain()
    {
        lock (Sync)
        {
            var list = new List<AudioEventPayload>(Pending.Count);
            while (Pending.Count > 0)
            {
                list.Add(Pending.Dequeue());
            }

            return list;
        }
    }
}

public static class AudioSourcePlayPatch
{
    public static void Postfix(AudioSource __instance)
    {
        if (__instance == null || !BridgeRuntime.IsRunning)
        {
            return;
        }

        var clip = __instance.clip;
        AudioTrackerService.Enqueue(new AudioEventPayload
        {
            clipName = clip != null ? clip.name : "<none>",
            sourcePath = GetPath(__instance.gameObject),
            volume = __instance.volume
        });
    }

    private static string GetPath(GameObject go)
    {
        var parts = new List<string>();
        var current = go.transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}
