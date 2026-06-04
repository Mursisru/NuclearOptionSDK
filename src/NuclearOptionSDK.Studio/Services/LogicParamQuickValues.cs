namespace NuclearOptionSDK.Studio.Services;

public static class LogicParamQuickValues
{
    public static IReadOnlyList<string> ForField(LogicParamField field)
    {
        if (field.Choices?.Count > 0)
        {
            return field.Choices;
        }

        return field.Key switch
        {
            "threshold" or "min" or "max" or "onThreshold" or "offThreshold"
                => ["0", "5", "10", "15", "20", "25", "30", "45", "60"],
            "seconds" or "delaySec" or "intervalSec"
                => ["0.1", "0.25", "0.5", "1", "2", "3", "5"],
            "volume" => ["0", "0.25", "0.5", "0.75", "1"],
            "fontSize" => ["10", "12", "14", "16", "18", "22"],
            "colorHtml" => ["#FFFFFF", "#FF0000", "#FF4400", "#FFAA00", "#00FF88", "#44AAFF", "#AA44FF"],
            "labelId" => ["aoa-label", "speed-label", "fuel-label", "alt-label", "g-label"],
            "instanceId" => ["aoa-text", "aoa-root", "speed-text", "hud-root"],
            "clipName" => ["stallHorn", "lockTone", "warningBeep", "clickUI"],
            "bindingId" => ["Cockpit.velocity", "Member.AeroPart.OnInitialize", "Telemetry.AoA"],
            "visible" or "active" => ["true", "false"],
            _ => Array.Empty<string>()
        };
    }
}
