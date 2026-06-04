namespace NuclearOptionSDK.Decompiler;

/// <summary>
/// Heuristics for dump preview snippets vs full decompiled method bodies.
/// </summary>
public static class PreviewTextPolicy
{
    public const int MinStrongLineCount = 4;
    public const int MinStrongCharCount = 80;

    public static bool IsWeakPreview(string? text, string? signature = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (signature != null && string.Equals(trimmed, signature.Trim(), StringComparison.Ordinal))
        {
            return true;
        }

        var lines = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < MinStrongLineCount && trimmed.Length < MinStrongCharCount)
        {
            return true;
        }

        if (trimmed.Contains(" RVA: ", StringComparison.Ordinal) && !trimmed.Contains('{'))
        {
            return true;
        }

        if (lines.Length == 1 && !trimmed.Contains('{') && trimmed.EndsWith(';'))
        {
            return true;
        }

        return false;
    }
}
