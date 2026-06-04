using System.Reflection;
using System.Text.Json;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Filtering;

public static class ApiSurfaceRulesLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ApiSurfaceRules Load()
    {
        var rules = LoadEmbeddedDefaults() ?? new ApiSurfaceRules();
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NuclearOptionSDK",
            "api-surface",
            "rules.json");
        if (!File.Exists(appDataPath))
        {
            return rules;
        }

        try
        {
            var json = File.ReadAllText(appDataPath);
            return JsonSerializer.Deserialize<ApiSurfaceRules>(json, JsonOptions) ?? rules;
        }
        catch
        {
            return rules;
        }
    }

    private static ApiSurfaceRules? LoadEmbeddedDefaults()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "Defaults", "api-surface-rules.json");
        if (!File.Exists(path))
        {
            return new ApiSurfaceRules();
        }

        try
        {
            return JsonSerializer.Deserialize<ApiSurfaceRules>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return new ApiSurfaceRules();
        }
    }
}
