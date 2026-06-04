namespace NuclearOptionSDK.Studio.Services.ApiSurface.Models;

public sealed class ApiTypeModel
{
    public required string FullName { get; init; }
    public required string Namespace { get; init; }
    public required string Name { get; init; }
    public required string? BaseTypeFullName { get; init; }
    public required bool IsEnum { get; init; }
    public required int PriorityScore { get; init; }
    public required string? DisplayTag { get; init; }
    public required ApiSurfaceCategory Category { get; init; }
    public required IReadOnlyList<ApiMemberModel> Members { get; init; }
}
