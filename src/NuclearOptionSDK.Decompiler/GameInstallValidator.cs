namespace NuclearOptionSDK.Decompiler;

public sealed record GameInstallValidation(bool IsValid, string Message, string? AssemblyPath);

public static class GameInstallValidator
{
    public static bool BypassValidation { get; set; }

    public static GameInstallValidation Validate(string? nuclearOptionRoot)
    {
        if (BypassValidation)
        {
            return new GameInstallValidation(true, "Bypass (smoke/tests).", null);
        }

        if (string.IsNullOrWhiteSpace(nuclearOptionRoot))
        {
            return new GameInstallValidation(
                false,
                "Укажите папку установки Nuclear Option (Steam → Nuclear Option).",
                null);
        }

        var root = nuclearOptionRoot.Trim();
        if (!Directory.Exists(root))
        {
            return new GameInstallValidation(
                false,
                $"Папка игры не найдена:\n{root}",
                null);
        }

        var assemblyPath = GameAssemblyPaths.ResolveMainAssembly(root);
        if (assemblyPath == null)
        {
            return new GameInstallValidation(
                false,
                "В папке игры нет Assembly-CSharp.dll.\nОжидается: NuclearOption_Data\\Managed\\Assembly-CSharp.dll",
                null);
        }

        return new GameInstallValidation(true, "Игра найдена.", assemblyPath);
    }
}
