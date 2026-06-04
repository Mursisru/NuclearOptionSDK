namespace NuclearOptionSDK.Studio.Services.ApiSurface.Models;

public sealed class ApiMemberModel
{
    public required ApiSymbolId Id { get; init; }
    public required string TechnicalName { get; init; }
    public required string Signature { get; init; }
    public required string ClrTypeName { get; init; }
    public required ApiMemberSource Source { get; init; }
    public required ApiSurfaceCategory Category { get; init; }
    public bool IsHidden { get; set; }
    public required int PriorityScore { get; init; }
    public string? DeclaringBaseType { get; init; }
    public string BindingId { get; init; } = string.Empty;
    public ClrTypeKind ClrKind { get; init; } = ClrTypeKind.Other;
    public MemberBehaviorBucket Behavior { get; init; } = MemberBehaviorBucket.Data;
}
