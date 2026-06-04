using System;
using NuclearOptionSDK.LogicCore;
using NuclearOptionSDK.Protocol;
using UnityEngine;

namespace NuclearOptionSDK.Bridge;

public sealed class TelemetryService : ITelemetryContext
{
    private readonly Dictionary<string, double> _floats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _bools = new(StringComparer.Ordinal);

    public void Refresh()
    {
        _floats.Clear();
        _bools.Clear();

        if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft == null)
        {
            return;
        }

        var aoa = ComputeAoA(aircraft);
        _floats["Telemetry.AoA"] = aoa;
        _floats["Telemetry.Speed"] = aircraft.speed;
        _floats["Telemetry.Altitude"] = aircraft.radarAlt;
        _floats["Telemetry.G"] = aircraft.gForce;
        _floats["Telemetry.Fuel"] = 1.0;
        _floats["Aircraft.speed"] = aircraft.speed;
        _floats["Aircraft.radarAlt"] = aircraft.radarAlt;
        _floats["Aircraft.gForce"] = aircraft.gForce;

        _bools["Gate.InFlight"] = aircraft.speed > 10f;
        _bools["Aircraft.airborne"] = aircraft.radarAlt > 0.2f;
    }

    public static double ComputeAoA(Aircraft aircraft)
    {
        if (aircraft.cockpit == null || aircraft.cockpit.rb == null)
        {
            return 0;
        }

        var local = aircraft.cockpit.transform.InverseTransformDirection(aircraft.cockpit.rb.velocity);
        return Mathf.Atan2(local.y, local.z) * -57.29578f;
    }

    public bool TryGetFloat(string bindingId, out double value) => _floats.TryGetValue(bindingId, out value);
    public bool TryGetBool(string bindingId, out bool value) => _bools.TryGetValue(bindingId, out value);

    public BindingWatchPayload Snapshot()
    {
        return new BindingWatchPayload
        {
            telemetry = new Dictionary<string, double>(_floats),
            flags = new Dictionary<string, bool>(_bools)
        };
    }
}
