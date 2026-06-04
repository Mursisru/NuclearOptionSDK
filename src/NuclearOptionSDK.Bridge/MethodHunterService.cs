using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace NuclearOptionSDK.Bridge;

public static class MethodHunterService
{
    public const string DefaultHarmonyId = "com.at747.nuclearoptionsdk.bridge.method-hunter";

    private const int MaxPatchedMethods = 2200;
    private const int MinLogIntervalMs = 80;

    private static readonly object Sync = new();
    private static readonly Dictionary<MethodBase, int> CallCounts = new();
    private static readonly Dictionary<MethodBase, long> LastLogAtMs = new();
    private static readonly List<MethodBase> PatchedMethods = new();
    private static readonly HashSet<string> ExcludedNameStartsWith = new(StringComparer.Ordinal)
    {
        "get_",
        "set_",
        "add_",
        "remove_",
        "OnGUI",
        "OnRender",
        "OnAudio",
        "Internal_",
        ".ctor"
    };

    private static readonly string[] ExcludedNameContains =
    {
        "Gizmos",
        "Profiler",
        "DebugDraw"
    };

    private static readonly string[] HighFrequencyMethodNames =
    {
        "Update",
        "LateUpdate",
        "FixedUpdate"
    };

    private static Harmony? _harmony;
    private static bool _armed;
    private static int _patchFailures;

    public static void EnsureInitialized(string harmonyId)
    {
        lock (Sync)
        {
            _harmony ??= new Harmony(harmonyId);
        }
    }

    public static void Arm()
    {
        EnsureInitialized(DefaultHarmonyId);

        Harmony? harmony;
        HarmonyMethod? prefix;
        lock (Sync)
        {
            if (_armed)
            {
                DisarmInternal();
            }

            harmony = _harmony;
            prefix = AccessTools.Method(typeof(MethodHunterService), nameof(MethodPrefix)) is { } prefixMethod
                ? new HarmonyMethod(prefixMethod)
                : null;
        }

        if (harmony == null)
        {
            LiveTraceService.Record("hunter", "MethodHunter", "init", "harmony=null");
            BridgeFileLogger.Error("trace.hunter", "Harmony instance is null");
            return;
        }

        if (prefix == null)
        {
            LiveTraceService.Record("hunter", "MethodHunter", "init", "prefix method not found");
            BridgeFileLogger.Error("trace.hunter", "MethodPrefix not found");
            return;
        }

        var assembly = ResolveGameAssembly();
        if (assembly == null)
        {
            LiveTraceService.Record("hunter", "MethodHunter", "init", "game assembly not found");
            BridgeFileLogger.Error("trace.hunter", "Assembly-CSharp not loaded");
            return;
        }

        var candidates = new List<MethodInfo>(MaxPatchedMethods);
        foreach (var type in EnumerateGameTypes(assembly))
        {
            if (!IsAllowedType(type))
            {
                continue;
            }

            foreach (var method in type.GetMethods(
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (candidates.Count >= MaxPatchedMethods)
                {
                    break;
                }

                if (!IsAllowedMethod(method))
                {
                    continue;
                }

                candidates.Add(method);
            }

            if (candidates.Count >= MaxPatchedMethods)
            {
                break;
            }
        }

        var patched = new List<MethodBase>(candidates.Count);
        var failures = 0;
        foreach (var method in candidates)
        {
            try
            {
                harmony.Patch(method, prefix: prefix);
                patched.Add(method);
            }
            catch
            {
                failures++;
            }
        }

        lock (Sync)
        {
            PatchedMethods.Clear();
            PatchedMethods.AddRange(patched);
            CallCounts.Clear();
            LastLogAtMs.Clear();
            _patchFailures = failures;
            _armed = patched.Count > 0;
        }

        var details = $"patched={patched.Count}; candidates={candidates.Count}; failed={failures}";
        LiveTraceService.Record("hunter", "MethodHunter", "armed", details);
        BridgeFileLogger.Info("trace.hunter", details);
        BridgeRuntime.Log?.LogInfo($"[MethodHunter] {details}");
    }

    public static void Disarm()
    {
        lock (Sync)
        {
            DisarmInternal();
        }
    }

    private static void DisarmInternal()
    {
        if (!_armed || _harmony == null)
        {
            return;
        }

        foreach (var method in PatchedMethods)
        {
            try
            {
                _harmony.Unpatch(method, HarmonyPatchType.Prefix, _harmony.Id);
            }
            catch
            {
                // Best-effort unpatch.
            }
        }

        var patchedCount = PatchedMethods.Count;
        PatchedMethods.Clear();
        CallCounts.Clear();
        LastLogAtMs.Clear();
        _armed = false;
        LiveTraceService.Record("hunter", "MethodHunter", "disarmed", $"unpatched={patchedCount}");
        BridgeFileLogger.Info("trace.hunter", $"disarmed unpatched={patchedCount}");
    }

    public static void MethodPrefix(MethodBase __originalMethod, object[]? __args)
    {
        if (!LiveTraceService.IsActive)
        {
            return;
        }

        var original = ResolveOriginalMethod(__originalMethod);
        if (original == null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int count;
        lock (Sync)
        {
            CallCounts.TryGetValue(original, out count);
            count++;
            CallCounts[original] = count;

            if (IsHighFrequencyMethod(original) && count > 3 && !IsPowerOfTwo(count))
            {
                return;
            }

            var shouldLog = count <= 3 || IsPowerOfTwo(count);
            if (!shouldLog)
            {
                return;
            }

            LastLogAtMs.TryGetValue(original, out var lastAt);
            if (now - lastAt < MinLogIntervalMs && count > 3)
            {
                return;
            }

            LastLogAtMs[original] = now;
        }

        var typeName = original.DeclaringType?.Name ?? "<global>";
        var details = $"count={count}";
        if (count <= 3)
        {
            details += "; args=" + FormatArgs(__args);
        }
        else
        {
            details += "; sampled=true";
        }

        LiveTraceService.Record("method", typeName, BuildMethodLabel(original), details);
    }

    private static MethodBase? ResolveOriginalMethod(MethodBase? injected)
    {
        if (injected != null)
        {
            return injected;
        }

        var trace = new StackTrace(false);
        for (var i = 0; i < trace.FrameCount && i < 12; i++)
        {
            var method = trace.GetFrame(i)?.GetMethod();
            if (method == null || method.DeclaringType == null)
            {
                continue;
            }

            if (method.DeclaringType == typeof(MethodHunterService))
            {
                continue;
            }

            var asm = method.DeclaringType.Assembly.GetName().Name;
            if (string.Equals(asm, "Assembly-CSharp", StringComparison.Ordinal))
            {
                return method;
            }
        }

        return null;
    }

    private static bool IsHighFrequencyMethod(MethodBase method)
    {
        foreach (var name in HighFrequencyMethodNames)
        {
            if (string.Equals(method.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Assembly? ResolveGameAssembly()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (string.Equals(name, "Assembly-CSharp", StringComparison.Ordinal))
            {
                return assembly;
            }
        }

        try
        {
            return Assembly.Load("Assembly-CSharp");
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<Type> EnumerateGameTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        foreach (var type in types)
        {
            if (type != null)
            {
                yield return type;
            }
        }
    }

    private static bool IsAllowedType(Type type)
    {
        if (type.IsGenericTypeDefinition || type.IsInterface)
        {
            return false;
        }

        var ns = type.Namespace ?? string.Empty;
        if (ns.StartsWith("UnityEngine", StringComparison.Ordinal) ||
            ns.StartsWith("TMPro", StringComparison.Ordinal) ||
            ns.StartsWith("System", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsAllowedMethod(MethodInfo method)
    {
        if (method.IsAbstract || method.ContainsGenericParameters || method.IsGenericMethodDefinition || method.IsSpecialName)
        {
            return false;
        }

        var name = method.Name;
        foreach (var prefix in ExcludedNameStartsWith)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        foreach (var marker in ExcludedNameContains)
        {
            if (name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
        }

        if (name.IndexOf("Render", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    private static string BuildMethodLabel(MethodBase method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return method.Name + "()";
        }

        var parts = new string[Math.Min(parameters.Length, 4)];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = parameters[i].ParameterType.Name;
        }

        var argsText = string.Join(",", parts);
        if (parameters.Length > parts.Length)
        {
            argsText += ",...";
        }

        return method.Name + "(" + argsText + ")";
    }

    private static string FormatArgs(object[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        var count = Math.Min(args.Length, 4);
        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            parts[i] = SafeValue(args[i]);
        }

        var text = "[" + string.Join(", ", parts);
        if (args.Length > count)
        {
            text += ", ...";
        }

        return text + "]";
    }

    private static string SafeValue(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        if (value is string s)
        {
            return "\"" + (s.Length > 40 ? s.Substring(0, 40) + "..." : s) + "\"";
        }

        var text = value.ToString() ?? value.GetType().Name;
        return text.Length > 64 ? text.Substring(0, 64) + "..." : text;
    }
}
