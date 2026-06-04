using NuclearOptionSDK.Studio.Services.ApiSurface.Filtering;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface;

public sealed class ApiSurfaceIndex
{
    private static ApiSurfaceRules? _rules;

    public static ApiSurfaceRules Rules => _rules ??= ApiSurfaceRulesLoader.Load();

    public static void ReloadRules() => _rules = ApiSurfaceRulesLoader.Load();

    public static IReadOnlyList<ApiTypeModel> Build(
        string nuclearOptionRoot,
        string? filter = null,
        ApiSurfaceRules? rulesOverride = null) =>
        CecilMetadataReader.ReadTypes(nuclearOptionRoot, rulesOverride ?? Rules, filter);
}
