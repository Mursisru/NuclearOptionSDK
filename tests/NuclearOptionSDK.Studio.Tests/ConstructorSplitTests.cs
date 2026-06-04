using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using NuclearOptionSDK.Studio.Views;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public class ConstructorSplitTests
{
    [Fact]
    public void ApplyConstructorSplitRatio_Sets_Star_Row_Weights()
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,6,*") };
        ConstructorSplitLayout.ApplyStarRows(grid, 0.6);

        Assert.Equal(GridUnitType.Star, grid.RowDefinitions[0].Height.GridUnitType);
        Assert.Equal(0.6, grid.RowDefinitions[0].Height.Value, 3);
        Assert.Equal(ConstructorSplitLayout.SplitterRowHeight, grid.RowDefinitions[1].Height.Value);
        Assert.Equal(0.4, grid.RowDefinitions[2].Height.Value, 3);
    }

    [Fact]
    public void ApplyPixelTop_Resizes_Rows()
    {
        var grid = new Grid
        {
            Height = 400,
            Width = 300,
            RowDefinitions = new RowDefinitions("*,6,*")
        };
        grid.Measure(new Size(300, 400));
        grid.Arrange(new Rect(0, 0, 300, 400));

        var resizer = new ConstructorSplitResizer(grid);
        resizer.ApplyPixelTop(120);

        Assert.Equal(GridUnitType.Pixel, grid.RowDefinitions[0].Height.GridUnitType);
        Assert.InRange(grid.RowDefinitions[0].Height.Value, 115, 125);
    }

    [Fact]
    public async Task Headless_Panel_Has_Visible_Split_Grip()
    {
        using var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StudioTestAppBuilder).Assembly);

        var failed = await session.Dispatch(() =>
        {
            var panel = new LogicConstructorPanel { Width = 700, Height = 500 };
            var window = new Window { Width = 700, Height = 500, Content = panel };
            window.Show();
            window.UpdateLayout();

            var splitter = panel.SplitGrip;
            if (splitter.Bounds.Height < 4 || splitter.Bounds.Width < 40)
            {
                return $"Splitter not laid out: {splitter.Bounds.Width}x{splitter.Bounds.Height}";
            }

            panel.ApplyConstructorSplitRatio(0.35);
            window.UpdateLayout();
            var ratio = panel.ReadSplitRatio();
            return Math.Abs(ratio - 0.35) < 0.08 ? null : $"ratio={ratio:F2}";
        }, CancellationToken.None);

        Assert.Null(failed);
    }
}
