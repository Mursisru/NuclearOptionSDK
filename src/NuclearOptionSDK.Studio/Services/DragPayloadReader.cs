using Avalonia.Input;
using NuclearOptionSDK.Studio.Views;

namespace NuclearOptionSDK.Studio.Services;

public static class DragPayloadReader
{
    public static bool HasStudioDrag(DragEventArgs e) =>
        e.DataTransfer.Contains(LogicPalettePanel.DragDataFormat)
        || e.DataTransfer.Contains(GameCodePanel.DragDataFormat);

    public static bool TryReadPalette(DragEventArgs e, out string kind, out string typeId)
    {
        kind = string.Empty;
        typeId = string.Empty;
        var payload = ReadString(e, LogicPalettePanel.DragDataFormat);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var parts = payload.Split('|', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        kind = parts[0];
        typeId = parts[1];
        return true;
    }

    public static bool TryReadGameCode(DragEventArgs e, out string kind, out string typeId, out string bindingId, out string? displayName)
    {
        kind = string.Empty;
        typeId = string.Empty;
        bindingId = string.Empty;
        displayName = null;

        var payload = ReadString(e, GameCodePanel.DragDataFormat);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var parts = StudioDragPayload.Decode(payload);
        if (parts.Length < 3)
        {
            return false;
        }

        kind = parts[0];
        typeId = parts[1];
        bindingId = parts[2];
        if (parts.Length > 3)
        {
            displayName = parts[3];
        }

        return !string.IsNullOrWhiteSpace(kind) && !string.IsNullOrWhiteSpace(typeId);
    }

    public static bool TryReadBinding(DragEventArgs e, out string bindingId, out string? displayName)
    {
        if (!TryReadGameCode(e, out _, out _, out bindingId, out displayName))
        {
            bindingId = string.Empty;
            displayName = null;
            return false;
        }

        return !string.IsNullOrWhiteSpace(bindingId);
    }

    private static string? ReadString(DragEventArgs e, DataFormat<string> format)
    {
        if (!e.DataTransfer.Contains(format))
        {
            return null;
        }

        return e.DataTransfer.TryGetValue(format);
    }
}
