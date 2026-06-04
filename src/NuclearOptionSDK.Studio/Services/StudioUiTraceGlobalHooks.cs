using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Глобальные обработчики TopLevel: логирует каждое нажатие/отпускание и move при capture или над split.</summary>
public static class StudioUiTraceGlobalHooks
{
    public static void Install(TopLevel topLevel)
    {
#if STUDIO_UI_TRACE
        if (!StudioUiInteractionTrace.IsEnabled || ReferenceEquals(_topLevel, topLevel))
        {
            return;
        }

        Uninstall();
        _topLevel = topLevel;
        const RoutingStrategies routes = RoutingStrategies.Tunnel | RoutingStrategies.Bubble;

        topLevel.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, routes, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, routes, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, routes, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel, routes, handledEventsToo: true);
        topLevel.AddHandler(InputElement.KeyDownEvent, OnKeyDown, routes, handledEventsToo: true);

        StudioUiInteractionTrace.Log("hooks", $"Installed on {Describe(topLevel)}");
#endif
    }

    public static void Uninstall()
    {
#if STUDIO_UI_TRACE
        if (_topLevel == null)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _topLevel.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        _topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        _topLevel.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel);
        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _topLevel = null;
        _hasLastMove = false;
#endif
    }

#if STUDIO_UI_TRACE
    private static TopLevel? _topLevel;
    private static Point _lastMoveLogged;
    private static bool _hasLastMove;

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetPosition((Visual?)sender ?? _topLevel!);
        var props = e.GetCurrentPoint(null).Properties;
        StudioUiInteractionTrace.Log("pointer.press",
            $"{PointerLine(e)} pos=({pt.X:F1},{pt.Y:F1}) btn={props.PointerUpdateKind} handled={e.Handled}");
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pt = e.GetPosition((Visual?)sender ?? _topLevel!);
        StudioUiInteractionTrace.Log("pointer.release",
            $"{PointerLine(e)} pos=({pt.X:F1},{pt.Y:F1}) kind={e.InitialPressMouseButton} handled={e.Handled}");
        _hasLastMove = false;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ShouldLogMove(e))
        {
            return;
        }

        var pt = e.GetPosition((Visual?)sender ?? _topLevel!);
        if (_hasLastMove)
        {
            var dx = pt.X - _lastMoveLogged.X;
            var dy = pt.Y - _lastMoveLogged.Y;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5 && e.Pointer.Captured == null)
            {
                return;
            }
        }

        _lastMoveLogged = pt;
        _hasLastMove = true;
        StudioUiInteractionTrace.Log("pointer.move",
            $"{PointerLine(e)} pos=({pt.X:F1},{pt.Y:F1}) handled={e.Handled}");
    }

    private static void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        var pt = e.GetPosition((Visual?)sender ?? _topLevel!);
        StudioUiInteractionTrace.Log("pointer.wheel",
            $"{Describe(e.Source as Control)} delta=({e.Delta.X:F2},{e.Delta.Y:F2}) pos=({pt.X:F1},{pt.Y:F1})");
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        StudioUiInteractionTrace.Log("key.down",
            $"{Describe(e.Source as Control)} key={e.Key} mods={e.KeyModifiers} handled={e.Handled}");
    }

    private static bool ShouldLogMove(PointerEventArgs e)
    {
        if (e.Pointer.Captured != null)
        {
            return true;
        }

        if (e.Source is Control c && IsInterestingForMove(c))
        {
            return true;
        }

        return false;
    }

    private static bool IsInterestingForMove(Control c)
    {
        if (c is Avalonia.Controls.Primitives.Thumb)
        {
            return true;
        }

        var id = Avalonia.Automation.AutomationProperties.GetAutomationId(c);
        if (!string.IsNullOrEmpty(id) && id.Contains("ConstructorSplit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return c.GetSelfAndVisualAncestors().OfType<Control>()
            .Any(a =>
            {
                var aid = Avalonia.Automation.AutomationProperties.GetAutomationId(a);
                return (!string.IsNullOrEmpty(aid) && aid.Contains("ConstructorSplit", StringComparison.OrdinalIgnoreCase))
                       || a is Avalonia.Controls.Primitives.Thumb;
            });
    }

    private static string PointerLine(PointerEventArgs e)
    {
        var cap = e.Pointer.Captured as Control;
        return $"src={Describe(e.Source as Control)} cap={Describe(cap)}";
    }

    private static string Describe(Control? control)
    {
        if (control == null)
        {
            return "null";
        }

        var name = string.IsNullOrEmpty(control.Name) ? "-" : control.Name;
        var id = Avalonia.Automation.AutomationProperties.GetAutomationId(control);
        if (string.IsNullOrEmpty(id))
        {
            id = "-";
        }

        var b = control.Bounds;
        return $"{control.GetType().Name}(Name={name},AutoId={id},Bounds={b.Width:F0}x{b.Height:F0})";
    }
#endif
}
