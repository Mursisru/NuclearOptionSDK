using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface;

public static class ApiSymbolIdFactory
{
    public static string MemberBindingId(string typeFullName, string memberName) =>
        $"Member.{typeFullName}.{memberName}";

    public static string MemberBindingId(ApiSymbolId id) =>
        MemberBindingId(id.TypeFullName, id.MemberKey);

    public static string? TryResolveReadId(string typeFullName, string memberName)
    {
        if (string.IsNullOrWhiteSpace(typeFullName) || string.IsNullOrWhiteSpace(memberName))
        {
            return null;
        }

        var shortType = ShortTypeName(typeFullName);
        var candidates = new[]
        {
            $"Read.{typeFullName}.{memberName}",
            $"Read.{shortType}.{memberName}",
            MemberBindingId(typeFullName, memberName)
        };

        foreach (var id in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (NoGameParameterCatalog.TryGet(id, out _) || NoGameParameterCatalog.TryGetMethod(id, out _))
            {
                return id;
            }
        }

        return MemberBindingId(typeFullName, memberName);
    }

    public static string ShortTypeName(string typeFullName) =>
        typeFullName.Contains('.')
            ? typeFullName[(typeFullName.LastIndexOf('.') + 1)..]
            : typeFullName;
}
