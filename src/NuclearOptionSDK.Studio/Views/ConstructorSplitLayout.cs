using Avalonia.Controls;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

/// <summary>Загрузка пропорций split — star rows, как <see cref="MainWindow"/> workspace.</summary>
public static class ConstructorSplitLayout
{
    public const double SplitterRowHeight = 6;

    public static void ApplyStarRows(Grid grid, double ratio)
    {
        if (grid.RowDefinitions.Count < 3)
        {
            return;
        }

        ratio = Math.Clamp(ratio,
            WorkspaceLayoutNormalizer.MinConstructorSplit,
            WorkspaceLayoutNormalizer.MaxConstructorSplit);

        grid.RowDefinitions[0] = new RowDefinition(new GridLength(ratio, GridUnitType.Star));
        grid.RowDefinitions[1] = new RowDefinition(SplitterRowHeight, GridUnitType.Pixel);
        grid.RowDefinitions[2] = new RowDefinition(new GridLength(1 - ratio, GridUnitType.Star));
    }
}
