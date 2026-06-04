using System.Collections.Generic;
using System.Linq;

namespace NuclearOptionSDK.LogicCore;

public interface ILogicNodeState
{
    bool EvaluateCrossedAbove(string nodeId, double value, double threshold);
    bool EvaluateCrossedBelow(string nodeId, double value, double threshold);
    bool EvaluateDelayBeforeShow(string nodeId, double delaySec);
    bool EvaluateBlink(string nodeId, double intervalSec);
    bool EvaluateCooldown(string nodeId, double cooldownSec);
    bool IsPostChainReady(string nodeId, double delaySec);
    bool EvaluateChanged(string nodeId, double value);
    void Tick(double deltaSec);
}

public sealed class NullLogicNodeState : ILogicNodeState
{
    public bool EvaluateCrossedAbove(string nodeId, double value, double threshold) => value > threshold;
    public bool EvaluateCrossedBelow(string nodeId, double value, double threshold) => value < threshold;
    public bool EvaluateDelayBeforeShow(string nodeId, double delaySec) => true;
    public bool EvaluateBlink(string nodeId, double intervalSec) => true;
    public bool EvaluateCooldown(string nodeId, double cooldownSec) => true;
    public bool IsPostChainReady(string nodeId, double delaySec) => true;
    public bool EvaluateChanged(string nodeId, double value) => false;
    public void Tick(double deltaSec) { }
}

public sealed class LogicStateStore : ILogicNodeState
{
    private readonly Dictionary<string, double> _timers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _lastValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _crossed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _cooldownUntil = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _blinkPhase = new(StringComparer.Ordinal);
    private double _now;

    public void Tick(double deltaSec)
    {
        _now += deltaSec;
        foreach (var key in _timers.Keys.ToList())
        {
            _timers[key] += deltaSec;
        }
    }

    public bool EvaluateCrossedAbove(string nodeId, double value, double threshold)
    {
        var wasBelow = !_lastValues.TryGetValue(nodeId, out var last) || last <= threshold;
        _lastValues[nodeId] = value;
        if (wasBelow && value > threshold)
        {
            _crossed[nodeId] = true;
            return true;
        }

        return _crossed.TryGetValue(nodeId, out var crossed) && crossed;
    }

    public bool EvaluateCrossedBelow(string nodeId, double value, double threshold)
    {
        var wasAbove = !_lastValues.TryGetValue(nodeId, out var last) || last >= threshold;
        _lastValues[nodeId] = value;
        if (wasAbove && value < threshold)
        {
            _crossed[nodeId] = true;
            return true;
        }

        return _crossed.TryGetValue(nodeId, out var crossed) && crossed;
    }

    public bool EvaluateDelayBeforeShow(string nodeId, double delaySec)
    {
        if (!_timers.ContainsKey(nodeId))
        {
            _timers[nodeId] = 0;
            return false;
        }

        return _timers[nodeId] >= delaySec;
    }

    public bool EvaluateBlink(string nodeId, double intervalSec)
    {
        if (!_timers.ContainsKey(nodeId))
        {
            _timers[nodeId] = 0;
            _blinkPhase[nodeId] = true;
        }

        if (_timers[nodeId] >= intervalSec)
        {
            _timers[nodeId] = 0;
            _blinkPhase[nodeId] = !(_blinkPhase.TryGetValue(nodeId, out var current) && current);
        }

        return _blinkPhase.TryGetValue(nodeId, out var on) && on;
    }

    public bool EvaluateCooldown(string nodeId, double cooldownSec)
    {
        if (_cooldownUntil.TryGetValue(nodeId, out var until) && _now < until)
        {
            return false;
        }

        _cooldownUntil[nodeId] = _now + cooldownSec;
        return true;
    }

    public bool IsPostChainReady(string nodeId, double delaySec)
    {
        if (!_timers.ContainsKey(nodeId))
        {
            _timers[nodeId] = 0;
            return false;
        }

        return _timers[nodeId] >= delaySec;
    }

    public bool EvaluateChanged(string nodeId, double value)
    {
        var changed = _lastValues.TryGetValue(nodeId, out var last) && Math.Abs(last - value) > 1e-9;
        _lastValues[nodeId] = value;
        return changed;
    }

    public void Reset()
    {
        _timers.Clear();
        _lastValues.Clear();
        _crossed.Clear();
        _cooldownUntil.Clear();
        _blinkPhase.Clear();
        _now = 0;
    }
}
