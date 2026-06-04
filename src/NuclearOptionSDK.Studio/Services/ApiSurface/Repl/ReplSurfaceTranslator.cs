using System.Text.RegularExpressions;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Repl;

public sealed class ReplSurfaceTranslator
{
    private static readonly Regex WordRegex = new(@"\b([a-zA-Z_][a-zA-Z0-9_]*)\b", RegexOptions.Compiled);

    private readonly ReplAliasStore _aliases;

    public ReplSurfaceTranslator(ReplAliasStore? aliases = null) =>
        _aliases = aliases ?? ReplAliasStore.LoadDefault();

    public ReplTranslationResult Translate(string friendlySource)
    {
        if (string.IsNullOrWhiteSpace(friendlySource))
        {
            return new ReplTranslationResult(friendlySource, friendlySource, Array.Empty<string>());
        }

        var replacements = new List<string>();
        var technical = WordRegex.Replace(friendlySource, match =>
        {
            var word = match.Groups[1].Value;
            if (_aliases.Aliases.TryGetValue(word, out var replacement))
            {
                replacements.Add($"{word} → {replacement}");
                return replacement;
            }

            return match.Value;
        });

        return new ReplTranslationResult(friendlySource, technical, replacements);
    }
}

public sealed record ReplTranslationResult(
    string FriendlySource,
    string TechnicalSource,
    IReadOnlyList<string> Replacements);
