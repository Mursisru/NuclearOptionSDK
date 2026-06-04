using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Context;

public static class OwnerContextClassifier
{
    public static OwnerContextKind ClassifyType(string typeShortName)
    {
        var n = typeShortName.ToLowerInvariant();
        if (n.Contains("gameplayui") || n.Contains("hud") || n.Contains("display")
            || n.Contains("gauge") || n.Contains("mfd") || n.Contains("ui") && !n.Contains("audio"))
        {
            return OwnerContextKind.UserInterface;
        }

        if (n.Contains("network") || n.Contains("server") || n.Contains("client")
            || n.Contains("multiplayer") || n.Contains("sync"))
        {
            return OwnerContextKind.Network;
        }

        if (n.Contains("weapon") || n.Contains("missile") || n.Contains("gun")
            || n.Contains("combat") || n.Contains("turret"))
        {
            return OwnerContextKind.Weapons;
        }

        if (n.Contains("aircraft") || n.Contains("unit") || n.Contains("cockpit")
            || n.Contains("flight") || n.Contains("pilot"))
        {
            return OwnerContextKind.Flight;
        }

        if (n.Contains("map") || n.Contains("tactical"))
        {
            return OwnerContextKind.Map;
        }

        if (n.Contains("audio") || n.Contains("music"))
        {
            return OwnerContextKind.Audio;
        }

        if (n.Contains("game") && n.Contains("manager"))
        {
            return OwnerContextKind.Session;
        }

        return OwnerContextKind.General;
    }

    public static string OwnerLabel(OwnerContextKind kind) => kind switch
    {
        OwnerContextKind.UserInterface => "UI",
        OwnerContextKind.Flight => "Flight",
        OwnerContextKind.Weapons => "Weapons",
        OwnerContextKind.Network => "Network",
        OwnerContextKind.Session => "Session",
        OwnerContextKind.Audio => "Audio",
        OwnerContextKind.Map => "Map",
        _ => "General"
    };
}
