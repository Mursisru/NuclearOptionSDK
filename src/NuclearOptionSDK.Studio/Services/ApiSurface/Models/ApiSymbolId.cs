namespace NuclearOptionSDK.Studio.Services.ApiSurface.Models;

public readonly record struct ApiSymbolId(
    string TypeFullName,
    ApiMemberKind Kind,
    string MemberName,
    string? DeclaringTypeOverride = null,
    string? CompositionPath = null)
{
    public string MemberKey =>
        string.IsNullOrEmpty(CompositionPath)
            ? MemberName
            : $"{CompositionPath}.{MemberName}";
}
