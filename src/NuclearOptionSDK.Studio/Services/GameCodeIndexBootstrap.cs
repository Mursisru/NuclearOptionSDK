namespace NuclearOptionSDK.Studio.Services;

/// <summary>Loads Game Code index before logic-mod codegen so bindings resolve from the real API.</summary>
public static class GameCodeIndexBootstrap
{
    public static void EnsureLoaded(string? nuclearOptionRoot)
    {
        if (GameCodeIndexCache.IsLoaded
            || string.IsNullOrWhiteSpace(nuclearOptionRoot)
            || !Directory.Exists(Path.Combine(nuclearOptionRoot, "NuclearOption_Data", "Managed")))
        {
            return;
        }

        try
        {
            var types = GameCodeIndexService.LoadFromGame(nuclearOptionRoot);
            GameCodeIndexCache.SetIndex(types);
        }
        catch
        {
            // Codegen falls back to GameBindingRuntime reflection.
        }
    }
}
