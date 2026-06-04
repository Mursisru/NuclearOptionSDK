namespace NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;

public sealed class LabelResolverChain : ILabelResolver
{
    private readonly IReadOnlyList<ILabelResolver> _chain;

    public LabelResolverChain(params ILabelResolver[] chain) => _chain = chain;

    public bool TryResolveType(string typeFullName, out SymbolLabel label)
    {
        foreach (var resolver in _chain)
        {
            if (resolver.TryResolveType(typeFullName, out label) && !string.IsNullOrWhiteSpace(label.Title))
            {
                return true;
            }
        }

        label = default!;
        return false;
    }

    public bool TryResolveMember(string typeFullName, string memberName, out SymbolLabel label)
    {
        foreach (var resolver in _chain)
        {
            if (resolver.TryResolveMember(typeFullName, memberName, out label) && !string.IsNullOrWhiteSpace(label.Title))
            {
                return true;
            }
        }

        label = default!;
        return false;
    }
}
