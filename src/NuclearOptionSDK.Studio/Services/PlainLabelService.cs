using NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Deprecated facade — use <see cref="SymbolLabelService"/>.</summary>
public static class PlainLabelService
{
    public static string ForMember(string typeName, string memberName, string signature) =>
        SymbolLabelService.ForMember(typeName, memberName, signature);

    public static string ForType(string typeName) =>
        SymbolLabelService.ForType(typeName);
}
