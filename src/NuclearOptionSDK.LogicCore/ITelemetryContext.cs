using System.Collections.Generic;

namespace NuclearOptionSDK.LogicCore;

public interface ITelemetryContext
{
    bool TryGetFloat(string bindingId, out double value);
    bool TryGetBool(string bindingId, out bool value);
}

public sealed class DictionaryTelemetryContext : ITelemetryContext
{
    private readonly Dictionary<string, double> _floats;
    private readonly Dictionary<string, bool> _bools;

    public DictionaryTelemetryContext(
        Dictionary<string, double>? floats = null,
        Dictionary<string, bool>? bools = null)
    {
        _floats = floats ?? new Dictionary<string, double>();
        _bools = bools ?? new Dictionary<string, bool>();
    }

    public bool TryGetFloat(string bindingId, out double value) => _floats.TryGetValue(bindingId, out value);
    public bool TryGetBool(string bindingId, out bool value) => _bools.TryGetValue(bindingId, out value);
}
