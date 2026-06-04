using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public sealed class DockZoneManager
{
    public LogicUILayout Layout { get; private set; }

    public DockZoneManager(LogicUILayout? initial = null)
    {
        Layout = initial ?? new LogicUILayout { splitRatio = 0.5 };
    }

    public void SetSplitRatio(double ratio)
    {
        Layout.splitRatio = Math.Clamp(ratio,
            WorkspaceLayoutNormalizer.MinConstructorSplit,
            WorkspaceLayoutNormalizer.MaxConstructorSplit);
    }

    public LogicZone AddZone(string kind, string title)
    {
        var zone = new LogicZone
        {
            id = Guid.NewGuid().ToString("N")[..8],
            kind = kind,
            title = title,
            dock = "right",
            width = 0.25,
            visible = true
        };
        Layout.zones = Layout.zones.Append(zone).ToArray();
        return zone;
    }

    public void RemoveZone(string id)
    {
        Layout.zones = Layout.zones.Where(z => z.id != id).ToArray();
    }

    public void RenameZone(string id, string title)
    {
        foreach (var zone in Layout.zones.Where(z => z.id == id))
        {
            zone.title = title;
        }
    }

    public void SetZoneVisible(string id, bool visible)
    {
        foreach (var zone in Layout.zones.Where(z => z.id == id))
        {
            zone.visible = visible;
        }
    }

    public void Persist() => LogicProjectStore.SaveLayout(Layout);

    public void Reload()
    {
        Layout = LogicProjectStore.LoadLayout();
    }
}
