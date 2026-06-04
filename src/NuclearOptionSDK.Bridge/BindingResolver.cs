using System;
using System.Reflection;
using NuclearOptionSDK.LogicCore;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Bridge;

public sealed class BindingResolver
{
    private readonly TelemetryService _telemetry;
    private readonly Dictionary<string, MemberInfo?> _cache = new(StringComparer.Ordinal);

    public BindingResolver(TelemetryService telemetry)
    {
        _telemetry = telemetry;
    }

    public BindingListPayload ListBindings()
    {
        return new BindingListPayload
        {
            bindings = new[]
            {
                new BindingDescriptor { id = "Telemetry.AoA", kind = "telemetry", path = "AoADisplay formula", valueType = "float" },
                new BindingDescriptor { id = "Telemetry.Speed", kind = "telemetry", path = "Aircraft.speed", valueType = "float" },
                new BindingDescriptor { id = "Telemetry.Altitude", kind = "telemetry", path = "Aircraft.radarAlt", valueType = "float" },
                new BindingDescriptor { id = "Telemetry.G", kind = "telemetry", path = "Aircraft.gForce", valueType = "float" },
                new BindingDescriptor { id = "Aircraft.speed", kind = "member", path = "Aircraft.speed", valueType = "float" },
                new BindingDescriptor { id = "Aircraft.radarAlt", kind = "member", path = "Aircraft.radarAlt", valueType = "float" },
                new BindingDescriptor { id = "Aircraft.gForce", kind = "member", path = "Aircraft.gForce", valueType = "float" },
                new BindingDescriptor { id = "Gate.InFlight", kind = "telemetry", path = "speed>10", valueType = "bool" }
            }
        };
    }

    public BindingResolveResponse Resolve(BindingResolveRequest request)
    {
        if (_telemetry.TryGetFloat(request.bindingId, out var f))
        {
            return new BindingResolveResponse { success = true, bindingId = request.bindingId, floatValue = f };
        }

        if (_telemetry.TryGetBool(request.bindingId, out var b))
        {
            return new BindingResolveResponse { success = true, bindingId = request.bindingId, boolValue = b };
        }

        if (TryResolveMember(request.bindingId, out var memberValue))
        {
            return new BindingResolveResponse { success = true, bindingId = request.bindingId, floatValue = memberValue };
        }

        return new BindingResolveResponse { success = false, bindingId = request.bindingId, error = "Binding not found." };
    }

    private bool TryResolveMember(string path, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var resolvePath = path;
        if (resolvePath.StartsWith("Member.", StringComparison.Ordinal))
        {
            resolvePath = resolvePath.Substring("Member.".Length);
        }

        if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft == null)
        {
            return false;
        }

        try
        {
            object? current = aircraft;
            foreach (var segment in resolvePath.Split('.'))
            {
                if (current == null)
                {
                    return false;
                }

                if (segment is "Aircraft" or "aircraft")
                {
                    continue;
                }

                var type = current.GetType();
                var key = type.FullName + "." + segment;
                if (!_cache.TryGetValue(key, out var member))
                {
                    member = (MemberInfo?)type.GetField(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                             ?? type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _cache[key] = member;
                }

                if (member == null)
                {
                    return false;
                }

                current = member switch
                {
                    FieldInfo fi => fi.GetValue(current),
                    PropertyInfo pi => pi.GetValue(current),
                    _ => null
                };
            }

            return TryConvertNumeric(current, out value);
        }
        catch (Exception ex)
        {
            BridgeFileLogger.Error("binding.resolve", ex.Message);
        }

        return false;
    }

    private static bool TryConvertNumeric(object? current, out double value)
    {
        value = 0;
        switch (current)
        {
            case float f:
                value = f;
                return true;
            case double d:
                value = d;
                return true;
            case int i:
                value = i;
                return true;
            case bool b:
                value = b ? 1 : 0;
                return true;
            default:
                return false;
        }
    }
}
