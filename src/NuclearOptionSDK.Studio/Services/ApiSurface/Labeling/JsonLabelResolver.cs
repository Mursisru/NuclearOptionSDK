using NuclearOptionSDK.Studio.Services.ApiSurface;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;

public sealed class JsonLabelResolver : ILabelResolver
{
    private readonly JsonSymbolLabelStore _store;

    public JsonLabelResolver(JsonSymbolLabelStore store) => _store = store;

    public bool TryResolveType(string typeFullName, out SymbolLabel label)
    {
        if (_store.TryGetType(typeFullName, out label))
        {
            return true;
        }

        return _store.TryGetType(ApiSymbolIdFactory.ShortTypeName(typeFullName), out label);
    }

    public bool TryResolveMember(string typeFullName, string memberName, out SymbolLabel label)
    {
        var shortType = ApiSymbolIdFactory.ShortTypeName(typeFullName);
        var keys = new[]
        {
            $"{shortType}.{memberName}",
            $"{typeFullName}.{memberName}",
            memberName
        };

        foreach (var key in keys)
        {
            if (_store.TryGetMember(key, out label))
            {
                return true;
            }
        }

        label = default!;
        return false;
    }
}
