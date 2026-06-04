using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Taxonomy;

public static class KeywordCategoryAssigner
{
    public static ApiSurfaceCategory ForType(string typeName)
    {
        var n = typeName.ToLowerInvariant();
        if (n.Contains("weapon") || n.Contains("missile") || n.Contains("gun") || n.Contains("ammo"))
        {
            return ApiSurfaceCategory.Weapons;
        }

        if (n.Contains("radar") || n.Contains("ew") || n.Contains("jam") || n.Contains("rwr"))
        {
            return ApiSurfaceCategory.RadarEw;
        }

        if (n.Contains("hud") || n.Contains("display") || n.Contains("gauge") || n.Contains("mfd"))
        {
            return ApiSurfaceCategory.Hud;
        }

        if (n.Contains("map") || n.Contains("tactical"))
        {
            return ApiSurfaceCategory.Map;
        }

        if (n.Contains("audio") || n.Contains("music"))
        {
            return ApiSurfaceCategory.Audio;
        }

        if (n.Contains("aircraft") || n.Contains("flight") || n.Contains("cockpit") || n.Contains("unit"))
        {
            return ApiSurfaceCategory.FlightTelemetry;
        }

        return ApiSurfaceCategory.General;
    }

    public static ApiSurfaceCategory ForMember(string memberName, ApiSurfaceCategory typeCategory)
    {
        var n = memberName.ToLowerInvariant();
        if (n.Contains("fuel") || n.Contains("speed") || n.Contains("alt") || n.Contains("gforce") || n.Contains("aoa"))
        {
            return ApiSurfaceCategory.FlightTelemetry;
        }

        return typeCategory;
    }

    public static string? TypeTag(string typeName)
    {
        var cat = ForType(typeName);
        return cat switch
        {
            ApiSurfaceCategory.FlightTelemetry => "AVIATION",
            ApiSurfaceCategory.Weapons => "WEAPONS",
            ApiSurfaceCategory.RadarEw => "EW",
            ApiSurfaceCategory.Hud => "HUD",
            ApiSurfaceCategory.Map => "MAP",
            _ => null
        };
    }
}
