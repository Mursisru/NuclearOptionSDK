namespace NuclearOptionSDK.Studio.Services;

/// <summary>Normalizes action/method labels for display and Game Code search.</summary>
public static class ActionMethodLabel
{
    public static string StripCallSuffix(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return label ?? string.Empty;
        }

        var s = label.TrimEnd();
        if (s.EndsWith("()", StringComparison.Ordinal))
        {
            return s[..^2].Trim();
        }

        return s;
    }
}
