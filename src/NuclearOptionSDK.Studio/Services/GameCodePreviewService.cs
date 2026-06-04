using NuclearOptionSDK.Decompiler;

namespace NuclearOptionSDK.Studio.Services;

public sealed class GameCodePreviewService : IGameCodePreviewService
{
    private readonly IDecompileService _decompile;

    public GameCodePreviewService(IDecompileService? decompile = null)
    {
        _decompile = decompile ?? new IlSpyDecompileService();
    }

    public async Task<string> ResolvePreviewAsync(
        GameMemberNode member,
        GameTypeNode? ownerType,
        string? gameRoot,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        if (member.Kind != GameMemberKind.Method)
        {
            return member.Signature;
        }

        var typeName = ownerType?.FullName ?? InferTypeNameFromBinding(member.BindingId);
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return "// Could not resolve type for decompilation.";
        }

        var decompiled = await _decompile.DecompileMethodAsync(
            gameRoot,
            typeName,
            member.Name,
            bypassCache,
            cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(decompiled)
            ? member.Signature + Environment.NewLine + "// Decompilation failed (check game path and DLL version)."
            : decompiled;
    }

    public async Task<GameMemberNode?> ResolveBestMethodForTypeAsync(
        GameTypeNode type,
        string? gameRoot,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        foreach (var method in type.Methods.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var preview = await ResolvePreviewAsync(method, type, gameRoot, bypassCache, cancellationToken)
                .ConfigureAwait(false);
            if (!PreviewTextPolicy.IsWeakPreview(preview, method.Signature))
            {
                return WithPreview(method, preview);
            }
        }

        var first = type.Methods.FirstOrDefault();
        return first == null ? null : WithPreview(first, first.Signature);
    }

    public GameMemberNode WithPreview(GameMemberNode member, string previewText) => new()
    {
        Kind = member.Kind,
        Behavior = member.Behavior,
        Name = member.Name,
        Signature = member.Signature,
        BindingId = member.BindingId,
        SymbolId = member.SymbolId,
        Source = member.Source,
        Category = member.Category,
        ClrTypeName = member.ClrTypeName,
        ClrKind = member.ClrKind,
        OwnerContext = member.OwnerContext,
        Hint = member.Hint,
        ContextTitle = member.ContextTitle,
        InspectorLine = member.InspectorLine,
        CollisionBadge = member.CollisionBadge,
        SourceFile = member.SourceFile,
        SourceLine = member.SourceLine,
        PreviewText = previewText
    };

    private static string InferTypeNameFromBinding(string bindingId)
    {
        if (!bindingId.StartsWith("Member.", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var rest = bindingId["Member.".Length..];
        var lastDot = rest.LastIndexOf('.');
        return lastDot > 0 ? rest[..lastDot] : rest;
    }
}
