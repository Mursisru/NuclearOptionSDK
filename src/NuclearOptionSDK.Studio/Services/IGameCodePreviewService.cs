namespace NuclearOptionSDK.Studio.Services;

public interface IGameCodePreviewService
{
    Task<string> ResolvePreviewAsync(
        GameMemberNode member,
        GameTypeNode? ownerType,
        string? gameRoot,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    Task<GameMemberNode?> ResolveBestMethodForTypeAsync(
        GameTypeNode type,
        string? gameRoot,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    GameMemberNode WithPreview(GameMemberNode member, string previewText);
}
