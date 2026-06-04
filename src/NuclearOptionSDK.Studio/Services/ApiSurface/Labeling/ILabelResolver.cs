namespace NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;

public interface ILabelResolver
{
    bool TryResolveType(string typeFullName, out SymbolLabel label);
    bool TryResolveMember(string typeFullName, string memberName, out SymbolLabel label);
}
