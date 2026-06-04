using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

public partial class LogicPalettePanel : UserControl
{
    public const string DragFormat = "NuclearOptionSDK.LogicPalette";
    public static readonly DataFormat<string> DragDataFormat =
        DataFormat.CreateStringApplicationFormat(DragFormat);

    private const double DragThreshold = 7;

    private readonly DisplayLayerService _display = new();
    private readonly DebouncedAction _debouncedFilter;
    private List<PaletteRow> _templateRows = new();
    private string _categoryFilter = "*";
    private bool _dragActive;
    private Point? _pressPoint;
    private int _pressIndex = -1;
    private PointerPressedEventArgs? _pressArgs;

    public LogicPalettePanel()
    {
        InitializeComponent();
        _debouncedFilter = new DebouncedAction(ApplyFilter);
        CategoryBox.ItemsSource = new[]
        {
            "All",
            "Check",
            "Output",
            "Logic",
            "QOL"
        };
        CategoryBox.SelectedIndex = 0;
        CategoryBox.SelectionChanged += (_, _) =>
        {
            _categoryFilter = CategoryBox.SelectedIndex switch
            {
                1 => "check",
                2 => "action",
                3 => "logic",
                4 => "qol",
                _ => "*"
            };
            ApplyFilter();
        };
        FilterBox.TextChanged += (_, _) => _debouncedFilter.Trigger();
        BuildPalette();
    }

    public void SetCategoryFilter(string category)
    {
        _categoryFilter = category switch
        {
            "source" => "check",
            "gate" => "check",
            _ => category
        };
        CategoryBox.SelectedIndex = _categoryFilter switch
        {
            "check" => 1,
            "action" => 2,
            "logic" => 3,
            "qol" => 4,
            "audio" => 2,
            "mechanic" => 3,
            _ => 0
        };
        ApplyFilter();
    }

    private void BuildPalette()
    {
        _templateRows = new List<PaletteRow>();

        foreach (var check in LogicCheckCatalog.Palette)
        {
            _templateRows.Add(new PaletteRow(
                new DisplayEntry
                {
                    id = check.Id,
                    title = check.Title,
                    hint = check.Hint,
                    category = "check"
                },
                "check"));
        }

        foreach (var category in new[] { "action", "logic", "audio", "qol" })
        {
            foreach (var entry in _display.PaletteEntries(category))
            {
                _templateRows.Add(new PaletteRow(entry, GuessKind(entry)));
            }
        }

        ApplyFilter();
    }

    public static string GuessKind(DisplayEntry entry) => entry.category switch
    {
        "check" or "gate" => "check",
        "action" or "audio" => "output",
        "logic" or "mechanic" => "merge",
        _ => "merge"
    };

    private void ApplyFilter()
    {
        var filter = FilterBox.Text?.Trim() ?? string.Empty;
        IEnumerable<PaletteRow> rows = _templateRows;

        if (_categoryFilter != "*")
        {
            rows = _categoryFilter switch
            {
                "check" => rows.Where(r => r.Kind == "check"),
                _ => rows.Where(r => r.Entry.category == _categoryFilter)
            };
        }

        if (!string.IsNullOrEmpty(filter))
        {
            rows = rows
                .Select(r => new { Row = r, Score = Math.Max(
                    Math.Max(
                        FuzzySearchService.Score(r.Entry.title, filter),
                        FuzzySearchService.Score(r.Entry.id, filter)),
                    FuzzySearchService.Score(r.Entry.hint, filter)) })
                .Where(x => x.Score >= 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Row.Entry.title, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => x.Row);
        }
        else
        {
            rows = rows.OrderBy(r => r.Entry.title, StringComparer.CurrentCultureIgnoreCase);
        }

        var list = rows.Take(300).ToList();
        PaletteList.ItemsSource = list.Select(r => r.Entry.title).ToList();
        PaletteList.Tag = list;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        PaletteList.AddHandler(InputElement.PointerPressedEvent, OnPalettePointerPressed, handledEventsToo: true);
        PaletteList.AddHandler(InputElement.PointerMovedEvent, OnPalettePointerMoved, handledEventsToo: true);
        PaletteList.AddHandler(InputElement.PointerReleasedEvent, OnPalettePointerReleased, handledEventsToo: true);
    }

    private void OnPalettePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dragActive || e.GetCurrentPoint(PaletteList).Properties.IsLeftButtonPressed != true)
        {
            return;
        }

        var index = IndexAtPoint(e.GetPosition(PaletteList));
        if (index < 0)
        {
            return;
        }

        _pressPoint = e.GetPosition(PaletteList);
        _pressIndex = index;
        _pressArgs = e;
        PaletteList.SelectedIndex = index;
    }

    private async void OnPalettePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragActive || _pressPoint == null || _pressIndex < 0 || _pressArgs == null)
        {
            return;
        }

        if (e.GetCurrentPoint(PaletteList).Properties.IsLeftButtonPressed != true)
        {
            return;
        }

        var pos = e.GetPosition(PaletteList);
        var dx = pos.X - _pressPoint.Value.X;
        var dy = pos.Y - _pressPoint.Value.Y;
        if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
        {
            return;
        }

        if (PaletteList.Tag is not List<PaletteRow> rows || _pressIndex >= rows.Count)
        {
            return;
        }

        _dragActive = true;
        var row = rows[_pressIndex];
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DragDataFormat, $"{row.Kind}|{row.Entry.id}"));
        await DragDrop.DoDragDropAsync(_pressArgs, data, DragDropEffects.Copy);
        _dragActive = false;
        _pressPoint = null;
        _pressIndex = -1;
        _pressArgs = null;
    }

    private void OnPalettePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressPoint = null;
        _pressIndex = -1;
        _pressArgs = null;
    }

    private int IndexAtPoint(Point point)
    {
        for (var i = 0; i < PaletteList.ItemCount; i++)
        {
            if (PaletteList.ContainerFromIndex(i) is Control item)
            {
                var bounds = item.Bounds;
                if (bounds.Contains(point))
                {
                    return i;
                }
            }
        }

        return PaletteList.SelectedIndex;
    }

    private sealed class PaletteRow
    {
        public PaletteRow(DisplayEntry entry, string kind)
        {
            Entry = entry;
            Kind = kind;
        }

        public DisplayEntry Entry { get; }
        public string Kind { get; }
    }
}
