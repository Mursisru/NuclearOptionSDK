namespace NuclearOptionSDK.Decompiler;

public interface IDecompileService
{
    Task<string?> DecompileMethodAsync(
        string? nuclearOptionRoot,
        string typeFullName,
        string methodName,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    Task<string?> DecompileTypeAsync(
        string? nuclearOptionRoot,
        string typeFullName,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);
}
