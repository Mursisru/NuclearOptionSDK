using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

public partial class VisualHudEditorPanel : UserControl
{
    private readonly ObservableCollection<VisualHudLabelItem> _labels = new();
    private readonly ObservableCollection<VisualHudPrimitiveItem> _primitives = new();
    private object? _selected;
    private Control? _selectedVisual;
    private Point _dragStart;
    private bool _dragging;

    public event Action<string>? StatusChanged;
    public event Func<Task>? PreviewRequested;
    public event Action<VisualHudSelection?>? SelectionChanged;

    public VisualHudEditorPanel()
    {
        InitializeComponent();
        AddLabelButton.Click += (_, _) => AddLabel();
        AddLineButton.Click += (_, _) => AddLine();
        AddCircleButton.Click += (_, _) => AddCircle();
        DeleteButton.Click += (_, _) => DeleteSelected();
        PushPreviewButton.Click += async (_, _) =>
        {
            if (PreviewRequested != null)
            {
                await PreviewRequested();
            }
        };
        SaveLayoutButton.Click += (_, _) => SaveLayout();
        LoadLayoutButton.Click += (_, _) => LoadLayout();
    }

    public IReadOnlyList<VisualHudLabelItem> Labels => _labels;
    public IReadOnlyList<VisualHudPrimitiveItem> Primitives => _primitives;

    public VisualHudLayoutPayload BuildLayoutPayload(string name = "visual-hud")
        => VisualHudDocument.ToPayload(name, _labels, _primitives);

    public void ApplyInspectorValues(VisualHudSelection values)
    {
        if (_selected is VisualHudLabelItem label)
        {
            label.Text = values.Text ?? label.Text;
            label.ColorHtml = values.ColorHtml;
            label.FontSize = values.FontSize;
            label.X = values.X;
            label.Y = values.Y;
            label.Visible = values.Visible;
            RefreshLabelVisual(label);
            NotifySelection(label);
            return;
        }

        if (_selected is VisualHudPrimitiveItem prim)
        {
            prim.ColorHtml = values.ColorHtml;
            prim.X1 = values.X;
            prim.Y1 = values.Y;
            prim.X2 = values.X2;
            prim.Y2 = values.Y2;
            prim.Radius = values.Radius;
            RefreshPrimitiveVisual(prim);
            NotifySelection(prim);
        }
    }

    public void DeleteSelected()
    {
        if (_selected is VisualHudLabelItem label)
        {
            _labels.Remove(label);
            RemoveVisual(label);
            ClearSelection();
            StatusChanged?.Invoke("Label deleted.");
            return;
        }

        if (_selected is VisualHudPrimitiveItem prim)
        {
            _primitives.Remove(prim);
            RemoveVisual(prim);
            ClearSelection();
            StatusChanged?.Invoke("Shape deleted.");
        }
    }

    private void AddLabel()
    {
        var item = new VisualHudLabelItem
        {
            Text = $"Label {_labels.Count + 1}",
            X = 40 + _labels.Count * 12,
            Y = 40 + _labels.Count * 12
        };
        _labels.Add(item);
        RenderLabel(item);
        SelectItem(item);
        StatusChanged?.Invoke($"Added label '{item.Text}'");
    }

    private void AddLine()
    {
        var item = new VisualHudPrimitiveItem
        {
            Kind = "line",
            X1 = 80 + _primitives.Count * 8,
            Y1 = 80,
            X2 = 220 + _primitives.Count * 8,
            Y2 = 180
        };
        _primitives.Add(item);
        RenderPrimitive(item);
        SelectItem(item);
        StatusChanged?.Invoke("Added line.");
    }

    private void AddCircle()
    {
        var item = new VisualHudPrimitiveItem
        {
            Kind = "circle",
            X1 = 120 + _primitives.Count * 10,
            Y1 = 120 + _primitives.Count * 10,
            Radius = 40
        };
        _primitives.Add(item);
        RenderPrimitive(item);
        SelectItem(item);
        StatusChanged?.Invoke("Added circle.");
    }

    private void RenderLabel(VisualHudLabelItem item)
    {
        var border = new Border
        {
            BorderBrush = Brushes.DimGray,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.Parse("#22000000")),
            Padding = new Thickness(4),
            Tag = item,
            [Canvas.LeftProperty] = item.X,
            [Canvas.TopProperty] = item.Y,
            IsVisible = item.Visible
        };

        border.Child = new TextBlock
        {
            Text = item.Text,
            FontSize = item.FontSize,
            Foreground = ParseBrush(item.ColorHtml)
        };

        WireDrag(border);
        EditorCanvas.Children.Add(border);
    }

    private void RenderPrimitive(VisualHudPrimitiveItem item)
    {
        Control visual = item.Kind == "circle"
            ? new Ellipse
            {
                Width = item.Radius * 2,
                Height = item.Radius * 2,
                Stroke = ParseBrush(item.ColorHtml),
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                Tag = item,
                [Canvas.LeftProperty] = item.X1 - item.Radius,
                [Canvas.TopProperty] = item.Y1 - item.Radius
            }
            : new Line
            {
                StartPoint = new Point(item.X1, item.Y1),
                EndPoint = new Point(item.X2, item.Y2),
                Stroke = ParseBrush(item.ColorHtml),
                StrokeThickness = 2,
                Tag = item
            };

        WireDrag(visual);
        EditorCanvas.Children.Add(visual);
    }

    private void WireDrag(Control visual)
    {
        visual.PointerPressed += OnPointerPressed;
        visual.PointerMoved += OnPointerMoved;
        visual.PointerReleased += OnPointerReleased;
    }

    private void SelectItem(object item)
    {
        _selected = item;
        _selectedVisual = FindVisual(item);
        HighlightSelection();
        NotifySelection(item);
    }

    private void ClearSelection()
    {
        _selected = null;
        _selectedVisual = null;
        HighlightSelection();
        SelectionChanged?.Invoke(null);
    }

    private void NotifySelection(object item)
    {
        SelectionChanged?.Invoke(item switch
        {
            VisualHudLabelItem label => new VisualHudSelection
            {
                Kind = "label",
                Id = label.Id,
                Text = label.Text,
                ColorHtml = label.ColorHtml,
                FontSize = label.FontSize,
                X = label.X,
                Y = label.Y,
                Visible = label.Visible
            },
            VisualHudPrimitiveItem prim => new VisualHudSelection
            {
                Kind = prim.Kind,
                Id = prim.Id,
                ColorHtml = prim.ColorHtml,
                X = prim.X1,
                Y = prim.Y1,
                X2 = prim.X2,
                Y2 = prim.Y2,
                Radius = prim.Radius
            },
            _ => null
        });
    }

    private void HighlightSelection()
    {
        foreach (var child in EditorCanvas.Children)
        {
            if (child is Border border)
            {
                border.BorderBrush = ReferenceEquals(border.Tag, _selected) ? Brushes.DeepSkyBlue : Brushes.DimGray;
            }
            else if (child is Line line)
            {
                line.StrokeThickness = ReferenceEquals(line.Tag, _selected) ? 3 : 2;
            }
            else if (child is Ellipse ellipse)
            {
                ellipse.StrokeThickness = ReferenceEquals(ellipse.Tag, _selected) ? 3 : 2;
            }
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.Tag == null)
        {
            return;
        }

        SelectItem(control.Tag);
        _dragging = true;
        _dragStart = e.GetPosition(EditorCanvas);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _selected == null || sender is not Control control)
        {
            return;
        }

        var pos = e.GetPosition(EditorCanvas);
        var dx = pos.X - _dragStart.X;
        var dy = pos.Y - _dragStart.Y;
        _dragStart = pos;

        switch (_selected)
        {
            case VisualHudLabelItem label:
                label.X = Math.Clamp(label.X + dx, 0, VisualHudDocument.CanvasWidth - 40);
                label.Y = Math.Clamp(label.Y + dy, 0, VisualHudDocument.CanvasHeight - 24);
                Canvas.SetLeft(control, label.X);
                Canvas.SetTop(control, label.Y);
                NotifySelection(label);
                break;
            case VisualHudPrimitiveItem prim:
                prim.X1 += dx;
                prim.Y1 += dy;
                if (prim.Kind == "line")
                {
                    prim.X2 += dx;
                    prim.Y2 += dy;
                }

                RefreshPrimitiveVisual(prim);
                NotifySelection(prim);
                break;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
        if (sender is Control control)
        {
            e.Pointer.Capture(null);
        }
    }

    private void RefreshLabelVisual(VisualHudLabelItem item)
    {
        var visual = FindVisual(item);
        if (visual is Border border)
        {
            border.IsVisible = item.Visible;
            Canvas.SetLeft(border, item.X);
            Canvas.SetTop(border, item.Y);
            if (border.Child is TextBlock textBlock)
            {
                textBlock.Text = item.Text;
                textBlock.FontSize = item.FontSize;
                textBlock.Foreground = ParseBrush(item.ColorHtml);
            }
        }
    }

    private void RefreshPrimitiveVisual(VisualHudPrimitiveItem item)
    {
        var old = FindVisual(item);
        if (old != null)
        {
            EditorCanvas.Children.Remove(old);
        }

        RenderPrimitive(item);
        _selectedVisual = FindVisual(item);
        HighlightSelection();
    }

    private Control? FindVisual(object item)
        => EditorCanvas.Children.FirstOrDefault(c => ReferenceEquals(c.Tag, item));

    private void RemoveVisual(object item)
    {
        var visual = FindVisual(item);
        if (visual != null)
        {
            EditorCanvas.Children.Remove(visual);
        }
    }

    private void SaveLayout()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NuclearOptionSDK",
            "layouts");
        Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, "visual-hud.json");
        VisualHudDocument.Save(path, "visual-hud", _labels, _primitives);
        StatusChanged?.Invoke($"Layout saved: {path}");
    }

    private void LoadLayout()
    {
        var path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NuclearOptionSDK",
            "layouts",
            "visual-hud.json");
        if (!File.Exists(path))
        {
            StatusChanged?.Invoke("No saved layout found.");
            return;
        }

        EditorCanvas.Children.Clear();
        _labels.Clear();
        _primitives.Clear();
        var (labels, primitives) = VisualHudDocument.Load(path);
        foreach (var item in labels)
        {
            _labels.Add(item);
            RenderLabel(item);
        }

        foreach (var item in primitives)
        {
            _primitives.Add(item);
            RenderPrimitive(item);
        }

        ClearSelection();
        StatusChanged?.Invoke($"Layout loaded ({_labels.Count} labels, {_primitives.Count} shapes).");
    }

    private static IBrush ParseBrush(string html)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(html));
        }
        catch
        {
            return Brushes.White;
        }
    }
}
