namespace NuclearOptionSDK.Studio.Services;

public static class FuzzySearchService
{
    public static bool Matches(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        text = text.Trim();
        query = query.Trim();
        if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.All(token => SubsequenceMatch(text, token) || text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    public static int Score(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        text = text.Trim();
        query = query.Trim();
        if (text.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1000;
        }

        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 800;
        }

        if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 600;
        }

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 1 && tokens.All(t => text.Contains(t, StringComparison.OrdinalIgnoreCase)))
        {
            return 500;
        }

        return SubsequenceMatch(text, query) ? 300 : -1;
    }

    private static bool SubsequenceMatch(string text, string query)
    {
        var ti = 0;
        foreach (var ch in query)
        {
            var found = false;
            while (ti < text.Length)
            {
                if (char.ToLowerInvariant(text[ti]) == char.ToLowerInvariant(ch))
                {
                    ti++;
                    found = true;
                    break;
                }

                ti++;
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
