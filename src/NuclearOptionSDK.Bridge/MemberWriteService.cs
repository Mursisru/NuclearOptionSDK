using System;
using NuclearOptionSDK.GameBindings;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Bridge;

/// <summary>Reflection write for Member.* bindings from logic output (uses shared GameBindingRuntime).</summary>
public static class MemberWriteService
{
    public static bool TryApply(LogicActionResult action)
    {
        if (!string.Equals(action.typeId, "Action.SetMemberBind", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(action.labelId))
        {
            return false;
        }

        return TryWrite(action.labelId!, action.text ?? action.visible?.ToString() ?? string.Empty);
    }

    public static bool TryWrite(string bindingId, string rawValue)
    {
        if (string.IsNullOrWhiteSpace(bindingId))
        {
            return false;
        }

        if (!GameBindingRuntime.TryGetLocalAircraft(out var aircraft) || aircraft == null)
        {
            BridgeFileLogger.Info("member.write", $"no aircraft binding={bindingId}");
            return false;
        }

        var converted = ConvertRawValue(rawValue, bindingId);
        if (converted == null)
        {
            return false;
        }

        if (GameBindingRuntime.ApplyWrite(aircraft, bindingId, converted))
        {
            BridgeFileLogger.Info("member.write", $"{bindingId}={converted}");
            LiveTraceService.Record(
                "logic.write",
                aircraft.GetType().Name,
                "ApplyWrite",
                $"{bindingId}={converted}");
            return true;
        }

        BridgeFileLogger.Info("member.write", $"failed binding={bindingId}");
        return false;
    }

    private static object? ConvertRawValue(string raw, string bindingId)
    {
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw is "1" or "yes")
        {
            return true;
        }

        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw is "0" or "no")
        {
            return false;
        }

        if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f))
        {
            return f;
        }

        if (int.TryParse(raw, out var i))
        {
            return i;
        }

        return raw;
    }
}
