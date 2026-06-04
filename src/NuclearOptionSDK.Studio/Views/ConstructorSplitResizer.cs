using Avalonia.Controls;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

/// <summary>Пропорции split dev/user. В TabControl звёздочные строки часто не совпадают с фактом — применяем pixel, когда известна высота.</summary>
public sealed class ConstructorSplitResizer
{
    public const double GripHeight = ConstructorSplitLayout.SplitterRowHeight;
    public const double MinPaneHeight = 72;

    private readonly Grid _grid;

    public ConstructorSplitResizer(Grid grid)
    {
        _grid = grid;
    }

    public double ReadRatio()
    {
        if (_grid.RowDefinitions.Count < 3)
        {
            return WorkspaceLayoutNormalizer.DefaultConstructorSplit;
        }

        var top = _grid.RowDefinitions[0].ActualHeight;
        var bottom = _grid.RowDefinitions[2].ActualHeight;
        var sum = top + bottom;
        return sum > 1 ? top / sum : WorkspaceLayoutNormalizer.DefaultConstructorSplit;
    }

    public void ApplyRatio(
        double ratio,
        double minShare = WorkspaceLayoutNormalizer.MinConstructorSplit,
        double maxShare = WorkspaceLayoutNormalizer.MaxConstructorSplit)
    {
        if (_grid.RowDefinitions.Count < 3)
        {
            return;
        }

        ratio = Math.Clamp(ratio, minShare, maxShare);
        var total = _grid.Bounds.Height;
        if (total >= GripHeight + MinPaneHeight * 2)
        {
            ApplyPixelTop((total - GripHeight) * ratio);
            return;
        }

        _grid.RowDefinitions[0] = new RowDefinition(new GridLength(ratio, GridUnitType.Star));
        _grid.RowDefinitions[1] = new RowDefinition(GripHeight, GridUnitType.Pixel);
        _grid.RowDefinitions[2] = new RowDefinition(new GridLength(1 - ratio, GridUnitType.Star));
        StudioUiInteractionTrace.Log("split.apply-ratio", $"ratio={ratio:F3} unit=star (pre-layout)");
        StudioUiInteractionTrace.LogGridRows("split.apply-ratio", _grid);
    }

    public void ApplyPixelTop(double topPx)
    {
        var total = _grid.Bounds.Height;
        if (total < GripHeight + MinPaneHeight * 2)
        {
            StudioUiInteractionTrace.Log("split.pixel", $"SKIP gridH={total:F0}");
            return;
        }

        topPx = Math.Clamp(topPx, MinPaneHeight, total - GripHeight - MinPaneHeight);
        var bottomPx = total - GripHeight - topPx;

        _grid.RowDefinitions[0] = new RowDefinition(topPx, GridUnitType.Pixel);
        _grid.RowDefinitions[1] = new RowDefinition(GripHeight, GridUnitType.Pixel);
        _grid.RowDefinitions[2] = new RowDefinition(bottomPx, GridUnitType.Pixel);

        var ratio = ReadRatio();
        StudioUiInteractionTrace.Log("split.pixel", $"topPx={topPx:F1} bottomPx={bottomPx:F1} ratio={ratio:F3}");
    }

    public void EnsureNotStuck()
    {
        var ratio = ReadRatio();
        var target = WorkspaceLayoutNormalizer.NormalizeConstructorSplit(ratio);
        if (Math.Abs(ratio - target) > 0.04)
        {
            StudioUiInteractionTrace.Log("split.unstick", $"reset actual={ratio:F3} -> {target:F3}");
            ApplyRatio(target);
        }
    }
}
