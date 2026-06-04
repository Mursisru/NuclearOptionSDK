using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace NuclearOptionSDK.Studio.Views;

public partial class LogicNodeCanvas : UserControl
{
    private const double HeaderHeight = 18;
    private const double PortHitRadius = 14;

    private LogicGraph _graph = new();
    private LogicGraph _baseGraph = new();
    private readonly List<LogicNode> _sandboxNodes = new();
    private readonly List<LogicEdge> _sandboxEdges = new();
    private HashSet<string> _lockedIds = new(StringComparer.Ordinal);

    private readonly DisplayLayerService _display = new();
    private readonly Dictionary<string, Control> _nodeVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _measuredHeights = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _measuredWidths = new(StringComparer.Ordinal);
    private readonly List<EdgeVisual> _edgeVisuals = new();
    private LogicNode? _selectedNode;
    private LogicEdge? _selectedEdge;
    private Control? _dragVisual;
    private Point _dragStart;
    private bool _dragging;
    private bool _isReadOnly;
    private WireDragState? _wireDrag;
    private PathShape? _wireGhost;
    private Ellipse? _highlightedPort;
    private double _zoom = 1.0;
    private readonly ScaleTransform _zoomTransform = new() { ScaleX = 1, ScaleY = 1 };

    public event Action<LogicNode?>? SelectionChanged;
    public event Action<LogicEdge?>? EdgeSelectionChanged;
    public event Action<string>? StatusChanged;
    public event Action? GraphChanged;
    public event Action? SandboxResetRequested;

    public LogicNodeCanvas()
    {
        InitializeComponent();
        GraphCanvas.RenderTransform = _zoomTransform;
        WireDragDrop(this);
        GraphCanvas.AddHandler(PointerMovedEvent, OnGlobalPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        GraphCanvas.AddHandler(PointerReleasedEvent, OnGlobalPointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        GraphCanvas.PointerReleased += OnCanvasPointerReleased;
        GraphCanvas.PointerMoved += OnCanvasPointerMoved;
        GraphCanvas.PointerPressed += OnCanvasPointerPressed;
        KeyDown += OnKeyDown;
        Focusable = true;

        ZoomInButton.Click += (_, _) => SetZoom(_zoom + 0.1);
        ZoomOutButton.Click += (_, _) => SetZoom(_zoom - 0.1);
        ZoomResetButton.Click += (_, _) => SetZoom(1.0);
        ClearSandboxButton.Click += (_, _) =>
        {
            if (SandboxResetRequested != null)
            {
                SandboxResetRequested.Invoke();
            }
            else
            {
                ClearSandbox();
            }
        };
        GraphScroller.PointerWheelChanged += OnGraphWheel;
    }

    private void WireDragDrop(InputElement element)
    {
        element.AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        element.AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public bool SandboxMode { get; set; }

    /// <summary>Read-only canvas: принимать drop для загрузки reference-графа (верхняя панель).</summary>
    public bool AllowReferenceDrop { get; set; }

    public event Action<string, string, string, string?>? ReferenceDrop;

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => _isReadOnly = value;
    }

    private bool CanEditGraph => !_isReadOnly || SandboxMode;

    private bool IsNodeLocked(string id) => SandboxMode && _lockedIds.Contains(id);

    private bool IsSandboxEdge(LogicEdge edge) =>
        SandboxMode && _sandboxEdges.Any(e => ReferenceEquals(e, edge));

    public LogicEdge? SelectedEdge => _selectedEdge;

    public void SetGraph(LogicGraph graph)
    {
        if (SandboxMode)
        {
            _baseGraph = graph ?? new LogicGraph();
            if (_baseGraph.nodes.Length > 0)
            {
                _baseGraph = ReferenceGraphLayout.NormalizeForDisplay(_baseGraph);
            }

            _sandboxNodes.Clear();
            _sandboxEdges.Clear();
            _lockedIds = _baseGraph.nodes.Select(n => n.id).ToHashSet(StringComparer.Ordinal);
            _graph = MergeSandboxGraph();
            ClearSandboxButton.IsVisible = true;
        }
        else
        {
            _graph = graph ?? new LogicGraph();
            if (_isReadOnly && _graph.nodes.Length > 0)
            {
                _graph = ReferenceGraphLayout.NormalizeForDisplay(_graph);
            }

            ClearSandboxButton.IsVisible = false;
        }

        _selectedNode = null;
        _selectedEdge = null;
        RebuildGraph();
    }

    public void ClearSandbox()
    {
        if (!SandboxMode)
        {
            return;
        }

        _sandboxNodes.Clear();
        _sandboxEdges.Clear();
        _graph = MergeSandboxGraph();
        RebuildGraph();
        StatusChanged?.Invoke("Sandbox cleared.");
    }

    public void HighlightDropTarget(string? bindingId, string? typeId, string? labelHint = null)
    {
        LogicNode? node = null;
        if (!string.IsNullOrWhiteSpace(bindingId))
        {
            node = _graph.nodes.FirstOrDefault(n => MatchesBinding(n, bindingId));
        }

        if (node == null && !string.IsNullOrWhiteSpace(typeId))
        {
            node = _graph.nodes.FirstOrDefault(n => string.Equals(n.typeId, typeId, StringComparison.Ordinal));
        }

        SetZoom(1.0);
        if (node != null && _nodeVisuals.TryGetValue(node.id, out var visual))
        {
            SelectNode(node, visual);
            ScrollNodeIntoView(node);
            return;
        }

        _selectedNode = null;
        SelectionChanged?.Invoke(null);
    }

    public string DescribeDropTarget(string? bindingId, string? typeId, string? labelHint = null)
    {
        LogicNode? node = null;
        if (!string.IsNullOrWhiteSpace(bindingId))
        {
            node = _graph.nodes.FirstOrDefault(n => MatchesBinding(n, bindingId));
        }

        if (node == null && !string.IsNullOrWhiteSpace(typeId))
        {
            node = _graph.nodes.FirstOrDefault(n => string.Equals(n.typeId, typeId, StringComparison.Ordinal));
        }

        if (node != null)
        {
            if (node.parameters.TryGetValue("displayName", out var dn) && !string.IsNullOrWhiteSpace(dn))
            {
                return dn;
            }

            return _display.Title(node.typeId);
        }

        if (!string.IsNullOrWhiteSpace(labelHint))
        {
            return labelHint;
        }

        return bindingId ?? typeId ?? string.Empty;
    }

    public bool HasDropTargetInGraph(string? bindingId, string? typeId)
    {
        if (!string.IsNullOrWhiteSpace(bindingId)
            && _graph.nodes.Any(n => MatchesBinding(n, bindingId)))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(typeId)
               && _graph.nodes.Any(n => string.Equals(n.typeId, typeId, StringComparison.Ordinal));
    }

    public LogicGraph GetGraph() => _graph;

    public void AddNodeFromPalette(string kind, string typeId, double x, double y, Dictionary<string, string>? parameters = null)
    {
        if (!CanEditGraph)
        {
            return;
        }

        var node = CreateNode(kind, typeId, x, y);
        if (parameters != null)
        {
            foreach (var kv in parameters)
            {
                node.parameters[kv.Key] = kv.Value;
            }
        }

        LogicNodeParameterSchema.MergeDefaults(node);

        if (SandboxMode)
        {
            _sandboxNodes.Add(node);
            _graph = MergeSandboxGraph();
        }
        else
        {
            AppendNode(node);
        }

        RebuildGraph();
        if (_nodeVisuals.TryGetValue(node.id, out var visual))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_nodeVisuals.ContainsKey(node.id))
                {
                    SelectNode(node, visual);
                }
            });
        }

        StatusChanged?.Invoke($"Added: {_display.Title(typeId)}");
        NotifyGraphChanged();
    }

    public bool AddEdge(string fromNodeId, string toNodeId, string fromPort, string toPort)
    {
        if (!CanEditGraph || fromNodeId == toNodeId)
        {
            return false;
        }

        if (_graph.nodes.All(n => n.id != fromNodeId) || _graph.nodes.All(n => n.id != toNodeId))
        {
            return false;
        }

        if (_graph.edges.Any(e =>
                e.fromNode == fromNodeId && e.toNode == toNodeId
                && e.fromPort == fromPort && e.toPort == toPort))
        {
            return false;
        }

        var edge = new LogicEdge
        {
            fromNode = fromNodeId,
            toNode = toNodeId,
            fromPort = fromPort,
            toPort = toPort,
            parameters = new Dictionary<string, string>()
        };

        if (SandboxMode)
        {
            _sandboxEdges.Add(edge);
            _graph = MergeSandboxGraph();
            RebuildGraph();
        }
        else
        {
            _graph.edges = _graph.edges.Append(edge).ToArray();
            AddEdgeVisual(edge);
        }

        LogicParamCatalog.TryAutoFillWatchParamFromEdge(_graph, edge);
        LogicOutputMemberWrite.TryAutoFillFromEdge(_graph, edge);
        var toNode = _graph.nodes.FirstOrDefault(n => n.id == edge.toNode);
        if (toNode != null && _selectedNode?.id == toNode.id)
        {
            SelectionChanged?.Invoke(toNode);
        }

        StatusChanged?.Invoke("Connection created.");
        NotifyGraphChanged();
        return true;
    }

    public void DuplicateSubgraph(LogicGraph source)
    {
        if (!CanEditGraph || source.nodes.Length == 0 || SandboxMode)
        {
            return;
        }

        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var newNodes = new List<LogicNode>(_graph.nodes);
        foreach (var node in source.nodes)
        {
            var copy = new LogicNode
            {
                id = Guid.NewGuid().ToString("N")[..8],
                kind = node.kind,
                typeId = node.typeId,
                x = node.x + 40,
                y = node.y + 40,
                parameters = new Dictionary<string, string>(node.parameters)
            };
            idMap[node.id] = copy.id;
            newNodes.Add(copy);
        }

        var newEdges = new List<LogicEdge>(_graph.edges);
        foreach (var edge in source.edges)
        {
            if (idMap.TryGetValue(edge.fromNode, out var from) && idMap.TryGetValue(edge.toNode, out var to))
            {
                newEdges.Add(new LogicEdge
                {
                    fromNode = from,
                    toNode = to,
                    fromPort = edge.fromPort,
                    toPort = edge.toPort,
                    parameters = new Dictionary<string, string>(edge.parameters)
                });
            }
        }

        _graph.nodes = newNodes.ToArray();
        _graph.edges = newEdges.ToArray();
        RebuildGraph();
        StatusChanged?.Invoke("Subgraph duplicated from reference.");
        NotifyGraphChanged();
    }

    public void UpdateSelectedParameters(Dictionary<string, string> parameters)
    {
        if (_selectedNode == null || IsNodeLocked(_selectedNode.id))
        {
            return;
        }

        ApplyParametersToNode(_selectedNode, parameters, notifySelection: false, notifyGraph: false);
    }

    public void ApplyNodeParameter(LogicNode node, string key, string value)
    {
        if (!CanEditGraph || IsNodeLocked(node.id))
        {
            return;
        }

        var target = _graph.nodes.FirstOrDefault(n => n.id == node.id);
        if (target == null)
        {
            return;
        }

        var merged = new Dictionary<string, string>(target.parameters, StringComparer.Ordinal)
        {
            [key] = value
        };
        ApplyParametersToNode(
            target,
            merged,
            notifySelection: target.id == _selectedNode?.id);
    }

    private void ApplyParametersToNode(
        LogicNode node,
        Dictionary<string, string> parameters,
        bool notifySelection = true,
        bool notifyGraph = true)
    {
        node.parameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
        LogicNodeParameterSchema.SyncLegacyKeys(node);
        RefreshNodeVisual(node);
        if (notifyGraph)
        {
            NotifyGraphChanged();
        }

        if (notifySelection && _selectedNode?.id == node.id)
        {
            SelectionChanged?.Invoke(node);
        }
    }

    private void RefreshNodeVisual(LogicNode node)
    {
        if (!_nodeVisuals.TryGetValue(node.id, out var oldVisual))
        {
            RebuildGraph();
            return;
        }

        var left = Canvas.GetLeft(oldVisual);
        var top = Canvas.GetTop(oldVisual);
        GraphCanvas.Children.Remove(oldVisual);

        var visual = CreateNodeVisual(node);
        _nodeVisuals[node.id] = visual;
        GraphCanvas.Children.Add(visual);
        Canvas.SetLeft(visual, left);
        Canvas.SetTop(visual, top);

        if (_dragging && _selectedNode?.id == node.id)
        {
            _dragVisual = visual;
        }

        UpdateAllEdges();
        ExpandCanvasBounds();
    }

    public void UpdateSelectedEdgePorts(string fromPort, string toPort, bool lockPorts = false)
    {
        if (_selectedEdge == null || (SandboxMode && !IsSandboxEdge(_selectedEdge)))
        {
            return;
        }

        _selectedEdge.fromPort = fromPort;
        _selectedEdge.toPort = toPort;
        if (lockPorts)
        {
            _selectedEdge.parameters["lockPorts"] = "true";
        }

        UpdateAllEdges();
    }

    public void DeleteSelectedNode()
    {
        if (!CanEditGraph || _selectedNode == null || IsNodeLocked(_selectedNode.id))
        {
            return;
        }

        var id = _selectedNode.id;
        if (SandboxMode)
        {
            _sandboxNodes.RemoveAll(n => n.id == id);
            _sandboxEdges.RemoveAll(e => e.fromNode == id || e.toNode == id);
            _graph = MergeSandboxGraph();
        }
        else
        {
            _graph.nodes = _graph.nodes.Where(n => n.id != id).ToArray();
            _graph.edges = _graph.edges.Where(e => e.fromNode != id && e.toNode != id).ToArray();
        }

        _selectedNode = null;
        RebuildGraph();
        SelectionChanged?.Invoke(null);
        StatusChanged?.Invoke("Node deleted.");
        NotifyGraphChanged();
    }

    public void DeleteSelectedEdge()
    {
        if (!CanEditGraph || _selectedEdge == null)
        {
            return;
        }

        if (SandboxMode && !IsSandboxEdge(_selectedEdge))
        {
            return;
        }

        var edge = _selectedEdge;
        if (SandboxMode)
        {
            _sandboxEdges.Remove(edge);
            _graph = MergeSandboxGraph();
        }
        else
        {
            _graph.edges = _graph.edges.Where(e => e != edge).ToArray();
        }

        _selectedEdge = null;
        RebuildGraph();
        EdgeSelectionChanged?.Invoke(null);
        StatusChanged?.Invoke("Edge deleted.");
        NotifyGraphChanged();
    }

    private LogicGraph MergeSandboxGraph() => new()
    {
        nodes = _baseGraph.nodes.Concat(_sandboxNodes).ToArray(),
        edges = _baseGraph.edges.Concat(_sandboxEdges).ToArray()
    };

    private void NotifyGraphChanged() => GraphChanged?.Invoke();

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.35, 2.5);
        _zoomTransform.ScaleX = _zoom;
        _zoomTransform.ScaleY = _zoom;
        ZoomLabel.Text = $"{(int)(_zoom * 100)}%";
    }

    private void OnGraphWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SetZoom(_zoom + (e.Delta.Y > 0 ? 0.1 : -0.1));
            e.Handled = true;
        }
    }

    private LogicNode CreateNode(string kind, string typeId, double x, double y)
    {
        var node = new LogicNode
        {
            id = Guid.NewGuid().ToString("N")[..8],
            kind = kind,
            typeId = typeId,
            x = (float)x,
            y = (float)y,
            parameters = LogicNodeParameterSchema.GetDefaults(typeId, kind)
        };
        LogicNodeParameterSchema.MergeDefaults(node);
        return node;
    }

    private void AppendNode(LogicNode node) => _graph.nodes = _graph.nodes.Append(node).ToArray();

    private void RebuildGraph()
    {
        GraphCanvas.Children.Clear();
        _nodeVisuals.Clear();
        _measuredHeights.Clear();
        _measuredWidths.Clear();
        _edgeVisuals.Clear();

        foreach (var node in _graph.nodes)
        {
            var visual = CreateNodeVisual(node);
            _nodeVisuals[node.id] = visual;
            GraphCanvas.Children.Add(visual);
            Canvas.SetLeft(visual, node.x - LogicEdgeRouting.PortOutset);
            Canvas.SetTop(visual, node.y - LogicEdgeRouting.PortOutset);
        }

        foreach (var edge in _graph.edges)
        {
            AddEdgeVisual(edge);
        }

        ExpandCanvasBounds();
    }

    private double ResolveNodeHeight(LogicNode node) =>
        _measuredHeights.TryGetValue(node.id, out var height)
            ? height
            : LogicEdgeRouting.GetNodeHeight(node, ResolveNodeWidth(node));

    private double ResolveNodeWidth(LogicNode node) =>
        _measuredWidths.TryGetValue(node.id, out var width)
            ? width
            : LogicEdgeRouting.EstimateNodeWidth(LogicEdgeRouting.GetDisplayLines(node));

    private static double MeasureTextWidth(string text, double fontSize)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var probe = new TextBlock
        {
            Text = text,
            FontSize = fontSize
        };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

    private double MeasureNodeWidth(LogicNode node)
    {
        var steps = LogicEdgeRouting.GetSteps(node);
        var fontSize = steps.Count > 1 ? 10d : 11d;
        var maxLineWidth = LogicNodeLayoutMetrics.CollectLayoutLines(node)
            .Select(line => MeasureTextWidth(line, fontSize))
            .DefaultIfEmpty(0)
            .Max();
        var chromeWidth = maxLineWidth + 2 * LogicEdgeRouting.TextHorizontalInset + 4 + 10;
        return LogicEdgeRouting.ClampNodeWidth(chromeWidth);
    }

    private string NodeDisplayText(LogicNode node) =>
        node.parameters.TryGetValue("displayName", out var dn) && !string.IsNullOrEmpty(dn)
            ? dn
            : _display.Title(node.typeId);

    private Control CreateNodeVisual(LogicNode node)
    {
        var accent = AccentForKind(node.kind);
        var selected = _selectedNode?.id == node.id;
        var locked = IsNodeLocked(node.id);
        var inset = LogicEdgeRouting.TextContentInset;
        var vInset = LogicEdgeRouting.TextVerticalInset;
        var nodeWidth = MeasureNodeWidth(node);
        var contentWidth = LogicEdgeRouting.BodyContentWidth(nodeWidth);
        _measuredWidths[node.id] = nodeWidth;

        var header = new Border
        {
            Height = LogicEdgeRouting.HeaderHeight,
            Background = accent,
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Child = new TextBlock
            {
                Text = locked ? $"{KindLabel(node.kind)} · ref" : KindLabel(node.kind),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(inset, 2, inset, 0),
                Foreground = Brushes.White,
                Opacity = 0.92
            }
        };

        var body = BuildNodeBody(node, contentWidth, inset, vInset);

        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), Width = nodeWidth };
        content.Children.Add(header);
        Grid.SetRow(body, 1);
        content.Children.Add(body);

        content.Measure(new Size(nodeWidth, double.PositiveInfinity));
        var nodeHeight = Math.Max(
            LogicEdgeRouting.NodeHeight,
            Math.Ceiling(content.DesiredSize.Height) + 2);
        _measuredHeights[node.id] = nodeHeight;

        var chrome = new Border
        {
            Width = nodeWidth,
            MinHeight = nodeHeight,
            Height = nodeHeight,
            ClipToBounds = false,
            Background = new SolidColorBrush(Color.Parse(locked ? "#151518" : "#1A1A22")),
            BorderBrush = selected ? new SolidColorBrush(Color.Parse("#7EB6FF")) : new SolidColorBrush(Color.Parse("#3A3A48")),
            BorderThickness = selected ? new Thickness(2) : new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Opacity = locked ? 0.88 : 1,
            BoxShadow = selected
                ? new BoxShadows(new BoxShadow { Blur = 12, Color = Color.FromArgb(80, 126, 182, 255) })
                : new BoxShadows(new BoxShadow { Blur = 6, OffsetY = 2, Color = Color.FromArgb(60, 0, 0, 0) }),
            Child = content,
            ContextMenu = BuildNodeContextMenu(node)
        };

        var portPad = LogicEdgeRouting.PortOutset;
        var hostWidth = nodeWidth + portPad * 2;
        var hostHeight = nodeHeight + portPad * 2;

        var ports = new Canvas
        {
            Width = hostWidth,
            Height = hostHeight,
            IsHitTestVisible = CanEditGraph,
            ZIndex = 10
        };
        AddPortDot(ports, node, "in", portPad, portPad + nodeHeight / 2);
        AddPortDot(ports, node, "out", portPad + nodeWidth, portPad + nodeHeight / 2);
        AddPortDot(ports, node, "top", portPad + nodeWidth / 2, portPad);
        AddPortDot(ports, node, "bottom", portPad + nodeWidth / 2, portPad + nodeHeight);

        var host = new Panel
        {
            Width = hostWidth,
            Height = hostHeight,
            ClipToBounds = false,
            Tag = node,
            IsHitTestVisible = true
        };
        Canvas.SetLeft(chrome, portPad);
        Canvas.SetTop(chrome, portPad);
        host.Children.Add(chrome);
        host.Children.Add(ports);

        if (CanEditGraph && !locked)
        {
            host.PointerPressed += OnNodePointerPressed;
        }
        else
        {
            host.PointerPressed += OnNodeInspectPointerPressed;
        }

        return host;
    }

    private void OnNodeInspectPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var host = FindNodeHost(sender as Control);
        if (host?.Tag is not LogicNode node)
        {
            return;
        }

        Focus();
        SelectNode(node, host);
        e.Handled = true;
    }

    private static Control? FindNodeHost(Control? control)
    {
        while (control != null)
        {
            if (control is Panel { Tag: LogicNode } panel)
            {
                return panel;
            }

            control = control.Parent as Control;
        }

        return null;
    }

    private void SelectNodeById(string nodeId, bool notifySelection = true)
    {
        var node = _graph.nodes.FirstOrDefault(n => n.id == nodeId);
        if (node != null && _nodeVisuals.TryGetValue(nodeId, out var visual))
        {
            SelectNode(node, visual, notifySelection);
        }
    }

    private static Border GetNodeChrome(Control visual)
    {
        if (visual is Border border)
        {
            return border;
        }

        if (visual is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border chrome && chrome.Child is Grid)
                {
                    return chrome;
                }
            }
        }

        throw new InvalidOperationException("Node visual has no chrome border.");
    }

    private Control BuildNodeBody(LogicNode node, double contentWidth, double hInset, double vInset)
    {
        var summary = LogicNodeParameterSchema.FormatSummary(node, _display);
        var steps = LogicEdgeRouting.GetSteps(node);
        if (steps.Count > 1)
        {
            var panel = new StackPanel { Name = "BodyStack" };
            if (!string.IsNullOrWhiteSpace(summary))
            {
                panel.Children.Add(CreateParamSummaryText(summary, contentWidth));
                panel.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 3, 0, 3),
                    Background = new SolidColorBrush(Color.Parse("#3A3A48"))
                });
            }

            for (var i = 0; i < steps.Count; i++)
            {
                if (i > 0)
                {
                    panel.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(0, 3, 0, 3),
                        Background = new SolidColorBrush(Color.Parse("#3A3A48"))
                    });
                }

                panel.Children.Add(new TextBlock
                {
                    Text = steps[i],
                    Width = contentWidth,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.Parse("#E8E8EE"))
                });
            }

            return new Border
            {
                Name = "BodyContainer",
                Padding = new Thickness(hInset, vInset, hInset, vInset),
                Child = panel
            };
        }

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Name = "BodyText",
            Text = NodeDisplayText(node),
            Width = contentWidth,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#E8E8EE"))
        });

        if (!string.IsNullOrWhiteSpace(summary))
        {
            body.Children.Add(CreateParamSummaryText(summary, contentWidth));
        }

        return new Border
        {
            Name = "BodyContainer",
            Padding = new Thickness(hInset, vInset, hInset, vInset),
            Child = body
        };
    }

    private static TextBlock CreateParamSummaryText(string summary, double contentWidth) => new()
    {
        Name = "ParamSummary",
        Text = summary,
        Width = contentWidth,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 9,
        Margin = new Thickness(0, 4, 0, 0),
        Opacity = 0.72,
        Foreground = new SolidColorBrush(Color.Parse("#B8C0D0"))
    };

    private ContextMenu BuildNodeContextMenu(LogicNode node)
    {
        if (IsNodeLocked(node.id))
        {
            var lockedMenu = new ContextMenu();
            lockedMenu.Items.Add(new MenuItem { Header = "Template (ref)", IsEnabled = false });
            var lockedParams = BuildParameterContextMenu(node);
            if (lockedParams != null)
            {
                lockedMenu.Items.Add(lockedParams);
            }

            lockedMenu.Items.Add(new MenuItem { Header = "You can wire and add your own blocks", IsEnabled = false });
            return lockedMenu;
        }

        if (!CanEditGraph)
        {
            return new ContextMenu
            {
                Items =
                {
                    new MenuItem { Header = "Read-only", IsEnabled = false }
                }
            };
        }

        var delete = new MenuItem { Header = "Delete block" };
        delete.Click += (_, _) =>
        {
            _selectedNode = node;
            DeleteSelectedNode();
        };

        var dup = new MenuItem { Header = "Duplicate" };
        dup.Click += (_, _) =>
        {
            var copy = CreateNode(node.kind, node.typeId, node.x + 24, node.y + 24);
            copy.parameters = new Dictionary<string, string>(node.parameters);
            if (SandboxMode)
            {
                _sandboxNodes.Add(copy);
                _graph = MergeSandboxGraph();
            }
            else
            {
                AppendNode(copy);
            }

            ReferenceGraphLayout.ResolveHorizontalOverlaps(_graph.nodes);
            RebuildGraph();
        };

        var menu = new ContextMenu();
        var paramMenu = BuildParameterContextMenu(node);
        if (paramMenu != null)
        {
            menu.Items.Add(paramMenu);
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(dup);
        menu.Items.Add(delete);
        return menu;
    }

    private MenuItem? BuildParameterContextMenu(LogicNode node)
    {
        var fields = LogicNodeParameterSchema.GetFields(node);
        if (fields.Count == 0)
        {
            return null;
        }

        var editable = CanEditGraph && !IsNodeLocked(node.id);
        var root = new MenuItem { Header = editable ? "Parameters" : "Parameters (ref)" };

        foreach (var field in fields)
        {
            if (field.Key == "displayName")
            {
                continue;
            }

            node.parameters.TryGetValue(field.Key, out var current);
            current ??= ResolveLegacyParam(node, field);
            current ??= field.Placeholder ?? string.Empty;

            var fieldMenu = new MenuItem
            {
                Header = $"{field.Label}: {FormatParamValue(field, current)}",
                IsEnabled = editable || field.Kind != LogicParamKind.Text
            };

            IReadOnlyList<string> presets = field.Kind switch
            {
                LogicParamKind.Choice => field.Choices ?? Array.Empty<string>(),
                LogicParamKind.Number when field.Key is "expectValue" or "threshold" or "min" or "max" or "onThreshold" or "offThreshold"
                    => LogicParamCatalog.QuickValuesForParam(LogicParamCatalog.ResolveContextParam(node)),
                LogicParamKind.Number when field.Key is "holdSeconds" or "seconds" or "delaySec" or "durationSec"
                    => LogicParamCatalog.QuickValuesForParam("time"),
                _ => Array.Empty<string>()
            };

            foreach (var preset in presets)
            {
                var captured = preset;
                var item = new MenuItem
                {
                    Header = FormatParamChoice(field, captured),
                    IsEnabled = editable
                };
                if (editable)
                {
                    item.Click += (_, _) => ApplyNodeParameter(node, field.Key, captured);
                }

                fieldMenu.Items.Add(item);
            }

            if (fieldMenu.Items.Count == 0)
            {
                fieldMenu.IsEnabled = false;
            }

            root.Items.Add(fieldMenu);
        }

        return root;
    }

    private static string ResolveLegacyParam(LogicNode node, LogicParamField field) =>
        field.Key switch
        {
            "expectValue" when node.parameters.TryGetValue("threshold", out var th) => th,
            "targetId" when node.parameters.TryGetValue("labelId", out var lid) => lid,
            "sourceParam" when node.parameters.TryGetValue("gameTarget", out var gt) => gt,
            "holdSeconds" when node.parameters.TryGetValue("seconds", out var sec) => sec,
            _ => string.Empty
        };

    private string FormatParamValue(LogicParamField field, string value) =>
        field.Kind switch
        {
            LogicParamKind.Bool => value == "true" ? "yes" : "no",
            LogicParamKind.Choice => _display.Title(value),
            _ => value
        };

    private string FormatParamChoice(LogicParamField field, string value) =>
        field.Kind switch
        {
            LogicParamKind.Choice when field.Key == "branch" => LogicParamCatalog.BranchLabel(value),
            LogicParamKind.Choice => LogicParamCatalog.FriendlyTitle(value),
            LogicParamKind.Bool => value == "true" ? "On" : "Off",
            LogicParamKind.Color => value,
            _ => value
        };

    private void AddPortDot(Canvas canvas, LogicNode node, string portName, double x, double y)
    {
        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(Color.Parse("#8899AA")),
            Stroke = new SolidColorBrush(Color.Parse("#22222A")),
            StrokeThickness = 1,
            Tag = new PortTag(node.id, portName),
            Cursor = new Cursor(StandardCursorType.Cross)
        };
        Canvas.SetLeft(dot, x - 5);
        Canvas.SetTop(dot, y - 5);
        if (CanEditGraph)
        {
            dot.PointerPressed += OnPortPointerPressed;
            dot.PointerEntered += OnPortPointerEntered;
            dot.PointerExited += OnPortPointerExited;
        }

        canvas.Children.Add(dot);
    }

    private void RefreshNodeContent(Control visual, LogicNode node)
    {
        var border = GetNodeChrome(visual);
        if (border.Child is not Grid root)
        {
            return;
        }

        foreach (var bodyContainer in root.Children.OfType<Border>().Where(b => b.Name == "BodyContainer"))
        {
            if (bodyContainer.Child is TextBlock text && text.Name == "BodyText")
            {
                text.Text = node.parameters.TryGetValue("displayName", out var dn) && !string.IsNullOrEmpty(dn)
                    ? dn
                    : _display.Title(node.typeId);
            }
        }
    }

    private void AddEdgeVisual(LogicEdge edge)
    {
        var isRefEdge = SandboxMode && !IsSandboxEdge(edge);
        var path = new PathShape
        {
            Stroke = _selectedEdge == edge
                ? new SolidColorBrush(Color.Parse("#7EB6FF"))
                : new SolidColorBrush(Color.Parse(isRefEdge ? "#4A5568" : "#6A7A90")),
            StrokeThickness = _selectedEdge == edge ? 2.5 : 2,
            Fill = null,
            Tag = edge,
            IsHitTestVisible = !SandboxMode,
            Cursor = new Cursor(StandardCursorType.Hand),
            ContextMenu = BuildEdgeContextMenu(edge)
        };

        if (CanEditGraph)
        {
            path.PointerPressed += OnEdgePointerPressed;
        }

        UpdateEdgeGeometry(edge, path);
        _edgeVisuals.Add(new EdgeVisual(edge, path));
        GraphCanvas.Children.Insert(0, path);
    }

    private ContextMenu BuildEdgeContextMenu(LogicEdge edge)
    {
        if (!CanEditGraph || (SandboxMode && !IsSandboxEdge(edge)))
        {
            return new ContextMenu
            {
                Items =
                {
                    new MenuItem { Header = SandboxMode ? "Template link (read-only)" : "Read-only", IsEnabled = false }
                }
            };
        }

        var unlock = new MenuItem { Header = "Auto ports" };
        unlock.Click += (_, _) =>
        {
            edge.parameters.Remove("lockPorts");
            UpdateAllEdges();
        };

        var delete = new MenuItem { Header = "Delete connection" };
        delete.Click += (_, _) =>
        {
            _selectedEdge = edge;
            DeleteSelectedEdge();
        };

        return new ContextMenu { Items = { unlock, delete } };
    }

    private void UpdateEdgeGeometry(LogicEdge edge, PathShape path)
    {
        var fromNode = _graph.nodes.FirstOrDefault(n => n.id == edge.fromNode);
        var toNode = _graph.nodes.FirstOrDefault(n => n.id == edge.toNode);
        if (fromNode == null || toNode == null)
        {
            return;
        }

        var fromW = ResolveNodeWidth(fromNode);
        var toW = ResolveNodeWidth(toNode);
        var fromH = ResolveNodeHeight(fromNode);
        var toH = ResolveNodeHeight(toNode);
        var (fromPort, toPort) = LogicEdgeRouting.ResolvePorts(edge, fromNode, toNode, fromW, toW, fromH, toH);
        var from = LogicEdgeRouting.PortPoint(fromNode.x, fromNode.y, fromPort, fromH, fromW);
        var to = LogicEdgeRouting.PortPoint(toNode.x, toNode.y, toPort, toH, toW);
        path.Data = LogicEdgeRouting.BuildGeometry(from, to, fromPort, toPort);
    }

    private void UpdateAllEdges()
    {
        foreach (var ev in _edgeVisuals)
        {
            UpdateEdgeGeometry(ev.Edge, ev.Path);
        }
    }

    private void UpdateEdgesForNode(string nodeId)
    {
        foreach (var ev in _edgeVisuals.Where(e => e.Edge.fromNode == nodeId || e.Edge.toNode == nodeId))
        {
            if (!LogicEdgeRouting.PortsLocked(ev.Edge))
            {
                UpdateEdgeGeometry(ev.Edge, ev.Path);
            }
        }
    }

    private PortTag? HitTestPort(Point canvasPos)
    {
        PortTag? best = null;
        var bestDist = double.MaxValue;

        foreach (var node in _graph.nodes)
        {
            var nodeH = ResolveNodeHeight(node);
            var nodeW = ResolveNodeWidth(node);
            foreach (var portName in new[] { "in", "out", "top", "bottom" })
            {
                var pt = LogicEdgeRouting.PortPoint(node.x, node.y, portName, nodeH, nodeW);
                var dx = canvasPos.X - pt.X;
                var dy = canvasPos.Y - pt.Y;
                var dist = dx * dx + dy * dy;
                if (dist <= PortHitRadius * PortHitRadius && dist < bestDist)
                {
                    bestDist = dist;
                    best = new PortTag(node.id, portName);
                }
            }
        }

        return best;
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanEditGraph || sender is not Control host || host.Tag is not LogicNode node || IsNodeLocked(node.id))
        {
            return;
        }

        Focus();
        SelectNode(node, host);
        _dragging = true;
        _dragVisual = host;
        _dragStart = e.GetPosition(GraphCanvas);
        e.Pointer.Capture(GraphCanvas);
        e.Handled = true;
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_wireDrag != null)
        {
            return;
        }

        if (e.Source is not Ellipse && e.Source is not PathShape)
        {
            _selectedEdge = null;
            EdgeSelectionChanged?.Invoke(null);
            HighlightEdges();
        }
    }

    private void OnPortPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanEditGraph || sender is not Ellipse ellipse || ellipse.Tag is not PortTag tag)
        {
            return;
        }

        Focus();
        _wireDrag = new WireDragState(tag.NodeId, tag.PortName);
        _wireGhost = new PathShape
        {
            Stroke = new SolidColorBrush(Color.Parse("#AACCFF")),
            StrokeThickness = 2,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 },
            Fill = null,
            IsHitTestVisible = false
        };
        GraphCanvas.Children.Add(_wireGhost);
        UpdateWireGhost(e.GetPosition(GraphCanvas));
        e.Pointer.Capture(GraphCanvas);
        e.Handled = true;
    }

    private void OnPortPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_wireDrag == null || sender is not Ellipse ellipse)
        {
            return;
        }

        SetPortHighlight(ellipse, true);
    }

    private void OnPortPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Ellipse ellipse)
        {
            SetPortHighlight(ellipse, false);
        }
    }

    private void SetPortHighlight(Ellipse ellipse, bool on)
    {
        if (on)
        {
            _highlightedPort = ellipse;
            ellipse.Fill = new SolidColorBrush(Color.Parse("#7EB6FF"));
            ellipse.Width = 12;
            ellipse.Height = 12;
        }
        else if (_highlightedPort == ellipse)
        {
            ellipse.Fill = new SolidColorBrush(Color.Parse("#8899AA"));
            ellipse.Width = 10;
            ellipse.Height = 10;
            _highlightedPort = null;
        }
    }

    private void UpdateWireGhost(Point cursor)
    {
        if (_wireDrag == null || _wireGhost == null)
        {
            return;
        }

        var fromNode = _graph.nodes.FirstOrDefault(n => n.id == _wireDrag.FromNodeId);
        if (fromNode == null)
        {
            return;
        }

        var fromW = ResolveNodeWidth(fromNode);
        var fromH = ResolveNodeHeight(fromNode);
        var from = LogicEdgeRouting.PortPoint(fromNode.x, fromNode.y, _wireDrag.FromPort, fromH, fromW);
        _wireGhost.Data = LogicEdgeRouting.BuildGeometry(from, cursor, _wireDrag.FromPort, "in");
    }

    private void CancelWireDrag()
    {
        if (_wireGhost != null)
        {
            GraphCanvas.Children.Remove(_wireGhost);
            _wireGhost = null;
        }

        if (_highlightedPort != null)
        {
            SetPortHighlight(_highlightedPort, false);
        }

        _wireDrag = null;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_wireDrag != null)
        {
            UpdateWireGhost(e.GetPosition(GraphCanvas));
            return;
        }

        ProcessNodeDrag(e.GetPosition(GraphCanvas));
    }

    private void OnGlobalPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (IsExternalPointerCapture(e.Pointer.Captured as Visual))
        {
            return;
        }

        if (_wireDrag != null)
        {
            UpdateWireGhost(e.GetPosition(GraphCanvas));
            return;
        }

        if (_dragging)
        {
            ProcessNodeDrag(e.GetPosition(GraphCanvas));
        }
    }

    private static bool IsExternalPointerCapture(Visual? captured)
    {
        if (captured == null)
        {
            return false;
        }

        var current = captured;
        while (current != null)
        {
            if (current is GridSplitter)
            {
                return true;
            }

            var autoId = Avalonia.Automation.AutomationProperties.GetAutomationId(current);
            if (!string.IsNullOrEmpty(autoId)
                && autoId.Contains("ConstructorSplit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.Parent as Visual;
        }

        return false;
    }

    private void ProcessNodeDrag(Point dragPos)
    {
        if (_wireDrag != null)
        {
            return;
        }

        if (!_dragging || _selectedNode == null || _dragVisual == null)
        {
            return;
        }

        var dx = (dragPos.X - _dragStart.X) / _zoom;
        var dy = (dragPos.Y - _dragStart.Y) / _zoom;
        _dragStart = dragPos;

        _selectedNode.x = (float)Math.Max(0, _selectedNode.x + dx);
        _selectedNode.y = (float)Math.Max(0, _selectedNode.y + dy);
        Canvas.SetLeft(_dragVisual, _selectedNode.x - LogicEdgeRouting.PortOutset);
        Canvas.SetTop(_dragVisual, _selectedNode.y - LogicEdgeRouting.PortOutset);
        UpdateEdgesForNode(_selectedNode.id);
        ExpandCanvasBounds();
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_wireDrag != null)
        {
            var pos = e.GetPosition(GraphCanvas);
            var targetTag = (_highlightedPort?.Tag as PortTag) ?? HitTestPort(pos);
            if (targetTag != null
                && (targetTag.NodeId != _wireDrag.FromNodeId || targetTag.PortName != _wireDrag.FromPort))
            {
                AddEdge(_wireDrag.FromNodeId, targetTag.NodeId, _wireDrag.FromPort, targetTag.PortName);
            }

            CancelWireDrag();
            e.Pointer.Capture(null);
            return;
        }

        FinishNodeDrag(e.Pointer);
    }

    private void OnGlobalPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsExternalPointerCapture(e.Pointer.Captured as Visual))
        {
            return;
        }

        if (e.Pointer.Captured != null && e.Pointer.Captured != GraphCanvas
            && !IsDescendantOf(GraphCanvas, e.Pointer.Captured as Visual))
        {
            return;
        }

        if (_wireDrag != null)
        {
            OnCanvasPointerReleased(sender, e);
            return;
        }

        if (_dragging)
        {
            FinishNodeDrag(e.Pointer);
        }
    }

    private void FinishNodeDrag(IPointer pointer)
    {
        if (_wireDrag != null)
        {
            return;
        }

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        _dragVisual = null;
        pointer.Capture(null);
        NotifyGraphChanged();
    }

    private static bool IsDescendantOf(Visual root, Visual? candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        var current = candidate;
        while (current != null)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }

            current = current.Parent as Visual;
        }

        return false;
    }

    private void OnEdgePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not PathShape path || path.Tag is not LogicEdge edge)
        {
            return;
        }

        _selectedEdge = edge;
        _selectedNode = null;
        EdgeSelectionChanged?.Invoke(edge);
        SelectionChanged?.Invoke(null);
        HighlightEdges();
        e.Handled = true;
    }

    private void HighlightEdges()
    {
        foreach (var ev in _edgeVisuals)
        {
            var selected = _selectedEdge == ev.Edge;
            var isRefEdge = SandboxMode && !IsSandboxEdge(ev.Edge);
            ev.Path.Stroke = selected
                ? new SolidColorBrush(Color.Parse("#7EB6FF"))
                : new SolidColorBrush(Color.Parse(isRefEdge ? "#4A5568" : "#6A7A90"));
            ev.Path.StrokeThickness = selected ? 2.5 : 2;
        }
    }

    private void SelectNode(LogicNode node, Control visual, bool notifySelection = true)
    {
        var wasSame = _selectedNode?.id == node.id;
        _selectedNode = node;
        _selectedEdge = null;
        EdgeSelectionChanged?.Invoke(null);

        foreach (var pair in _nodeVisuals)
        {
            var selected = pair.Key == node.id;
            var chrome = GetNodeChrome(pair.Value);
            chrome.BorderBrush = selected
                ? new SolidColorBrush(Color.Parse("#7EB6FF"))
                : new SolidColorBrush(Color.Parse("#3A3A48"));
            chrome.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            chrome.BoxShadow = selected
                ? new BoxShadows(new BoxShadow { Blur = 12, Color = Color.FromArgb(80, 126, 182, 255) })
                : new BoxShadows(new BoxShadow { Blur = 6, OffsetY = 2, Color = Color.FromArgb(60, 0, 0, 0) });
        }

        if (notifySelection && !wasSame)
        {
            SelectionChanged?.Invoke(node);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!CanEditGraph && AllowReferenceDrop)
        {
            if (e.DataTransfer.Contains(LogicPalettePanel.DragDataFormat)
                || e.DataTransfer.Contains(GameCodePanel.DragDataFormat)
                || DragPayloadReader.HasStudioDrag(e))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
            }

            return;
        }

        if (!CanEditGraph)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (e.DataTransfer.Contains(LogicPalettePanel.DragDataFormat)
            || e.DataTransfer.Contains(GameCodePanel.DragDataFormat)
            || DragPayloadReader.HasStudioDrag(e))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!CanEditGraph && AllowReferenceDrop)
        {
            TryHandleReferenceDrop(e);
            return;
        }

        if (e.Handled || !CanEditGraph)
        {
            return;
        }

        if (SandboxMode)
        {
            return;
        }

        try
        {
            var pos = e.GetPosition(GraphCanvas);

            if (e.DataTransfer.Contains(GameCodePanel.DragDataFormat))
            {
                var payload = e.DataTransfer.TryGetValue(GameCodePanel.DragDataFormat);
                if (string.IsNullOrEmpty(payload))
                {
                    return;
                }

                var parts = StudioDragPayload.Decode(payload);
                if (parts.Length >= 3)
                {
                    var parameters = new Dictionary<string, string>();
                    if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
                    {
                        parameters["displayName"] = parts[3];
                    }

                    if (!parts[1].StartsWith("Read.", StringComparison.Ordinal)
                        && !parts[1].StartsWith("Telemetry.", StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(parts[2]))
                    {
                        parameters["bindingId"] = parts[2];
                    }

                    if (parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]))
                    {
                        GameBindingValueSchema.ApplyClrTypeFromDrag(parameters, parts[4]);
                    }
                    else if (GameCodeDragContext.LastDraggedMember != null)
                    {
                        GameBindingValueSchema.ApplyClrTypeFromDrag(
                            parameters, GameCodeDragContext.LastDraggedMember.ClrTypeName);
                    }

                    AddNodeFromPalette(parts[0], parts[1], Math.Max(0, pos.X - 40), Math.Max(0, pos.Y - 20), parameters);
                }

                e.Handled = true;
                return;
            }

            if (!e.DataTransfer.Contains(LogicPalettePanel.DragDataFormat))
            {
                return;
            }

            var palettePayload = e.DataTransfer.TryGetValue(LogicPalettePanel.DragDataFormat);
            if (string.IsNullOrEmpty(palettePayload))
            {
                return;
            }

            var paletteParts = palettePayload.Split('|', 2);
            if (paletteParts.Length != 2)
            {
                return;
            }

            AddNodeFromPalette(paletteParts[0], paletteParts[1], Math.Max(0, pos.X - 40), Math.Max(0, pos.Y - 20));
            e.Handled = true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Drop error: {ex.Message}");
            StudioFileLogger.Error("logic-drop", ex.ToString());
        }
    }

    private void TryHandleReferenceDrop(DragEventArgs e)
    {
        try
        {
            if (e.DataTransfer.Contains(GameCodePanel.DragDataFormat))
            {
                var payload = e.DataTransfer.TryGetValue(GameCodePanel.DragDataFormat);
                if (string.IsNullOrEmpty(payload))
                {
                    return;
                }

                var parts = StudioDragPayload.Decode(payload);
                if (parts.Length >= 3)
                {
                    var display = parts.Length > 3 ? parts[3] : null;
                    ReferenceDrop?.Invoke(parts[0], parts[1], parts[2], display);
                    e.Handled = true;
                }

                return;
            }

            if (!e.DataTransfer.Contains(LogicPalettePanel.DragDataFormat))
            {
                return;
            }

            var palettePayload = e.DataTransfer.TryGetValue(LogicPalettePanel.DragDataFormat);
            if (string.IsNullOrEmpty(palettePayload))
            {
                return;
            }

            var paletteParts = palettePayload.Split('|', 2);
            if (paletteParts.Length == 2)
            {
                ReferenceDrop?.Invoke(paletteParts[0], paletteParts[1], string.Empty, null);
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Reference drop error: {ex.Message}");
            StudioFileLogger.Error("reference-drop", ex.ToString());
        }
    }

    private void ScrollNodeIntoView(LogicNode node)
    {
        var targetX = node.x * _zoom - GraphScroller.Viewport.Width / 3;
        var targetY = node.y * _zoom - GraphScroller.Viewport.Height / 3;
        GraphScroller.Offset = new Vector(
            Math.Max(0, targetX),
            Math.Max(0, targetY));
    }

    private static bool MatchesBinding(LogicNode node, string bindingId)
    {
        if (!node.parameters.TryGetValue("bindingId", out var bound))
        {
            return false;
        }

        var member = bindingId.Split('.').LastOrDefault() ?? bindingId;
        return string.Equals(bound, bindingId, StringComparison.OrdinalIgnoreCase)
               || bound.Contains(member, StringComparison.OrdinalIgnoreCase)
               || bindingId.Contains(bound, StringComparison.OrdinalIgnoreCase);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!CanEditGraph)
        {
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (_selectedEdge != null)
            {
                DeleteSelectedEdge();
            }
            else if (_selectedNode != null)
            {
                DeleteSelectedNode();
            }

            e.Handled = true;
        }
    }

    private void ExpandCanvasBounds()
    {
        if (_graph.nodes.Length == 0)
        {
            return;
        }

        var maxX = _graph.nodes.Max(n => n.x + ResolveNodeWidth(n)) + 80;
        var maxY = _graph.nodes.Max(n => n.y + ResolveNodeHeight(n)) + 80;
        GraphCanvas.Width = Math.Max(1400, maxX);
        GraphCanvas.Height = Math.Max(520, maxY);
    }

    private static IBrush AccentForKind(string kind) => kind switch
    {
        "source" => new SolidColorBrush(Color.Parse("#2B5876")),
        "check" or "gate" => new SolidColorBrush(Color.Parse("#6B6528")),
        "output" => new SolidColorBrush(Color.Parse("#7A3030")),
        "merge" => new SolidColorBrush(Color.Parse("#2E6B3A")),
        _ => new SolidColorBrush(Color.Parse("#444450"))
    };

    private static string KindLabel(string kind) => kind switch
    {
        "source" => "Source",
        "check" => "Check",
        "gate" => "Gate",
        "output" => "Output",
        "merge" => "Merge",
        _ => kind
    };

    private sealed class EdgeVisual
    {
        public EdgeVisual(LogicEdge edge, PathShape path)
        {
            Edge = edge;
            Path = path;
        }

        public LogicEdge Edge { get; }
        public PathShape Path { get; }
    }

    private sealed class PortTag
    {
        public PortTag(string nodeId, string portName)
        {
            NodeId = nodeId;
            PortName = portName;
        }

        public string NodeId { get; }
        public string PortName { get; }
    }

    private sealed class WireDragState
    {
        public WireDragState(string fromNodeId, string fromPort)
        {
            FromNodeId = fromNodeId;
            FromPort = fromPort;
        }

        public string FromNodeId { get; }
        public string FromPort { get; }
    }
}
