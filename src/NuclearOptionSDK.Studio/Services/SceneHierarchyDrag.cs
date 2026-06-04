using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Views;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Drag из Hierarchy (Scene) в конструктор — как из «Код игры».</summary>
public static class SceneHierarchyDrag
{
    private const double DragThreshold = 7;

    private static bool _dragActive;
    private static Point? _pressPoint;
    private static PointerPressedEventArgs? _pressArgs;
    private static GameObjectNode? _dragNode;

    public static void Enable(TreeView tree)
    {
        tree.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
        tree.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
        tree.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dragActive)
        {
            return;
        }

        var item = FindTreeViewItem(e.Source);
        if (item?.Tag is not GameObjectNode node)
        {
            return;
        }

        _dragNode = node;
        _pressPoint = e.GetPosition((Visual)sender!);
        _pressArgs = e;
    }

    private static async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragActive || _dragNode == null || _pressPoint == null || _pressArgs == null)
        {
            return;
        }

        if (e.GetCurrentPoint((Visual)sender!).Properties.IsLeftButtonPressed != true)
        {
            return;
        }

        var pos = e.GetPosition((Visual)sender!);
        var dx = pos.X - _pressPoint.Value.X;
        var dy = pos.Y - _pressPoint.Value.Y;
        if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
        {
            return;
        }

        _dragActive = true;
        try
        {
            var node = _dragNode;
            var bindingId = $"Scene.{node.id}";
            var displayName = node.name;
            var payload = StudioDragPayload.Encode("source", "Member.Bind", bindingId, displayName);
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(GameCodePanel.DragDataFormat, payload));
            await DragDrop.DoDragDropAsync(_pressArgs, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            StudioFileLogger.Error("scene-drag", ex.ToString());
        }
        finally
        {
            _dragActive = false;
            _dragNode = null;
            _pressPoint = null;
            _pressArgs = null;
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragActive)
        {
            _dragNode = null;
            _pressPoint = null;
            _pressArgs = null;
        }
    }

    private static TreeViewItem? FindTreeViewItem(object? source)
    {
        while (source is Control control)
        {
            if (control is TreeViewItem item)
            {
                return item;
            }

            source = control.Parent;
        }

        return null;
    }
}
