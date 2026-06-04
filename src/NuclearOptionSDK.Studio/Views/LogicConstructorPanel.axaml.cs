namespace NuclearOptionSDK.Studio.Views;



using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using NuclearOptionSDK.ModKit;

using NuclearOptionSDK.Protocol;

using NuclearOptionSDK.Studio.Services;



public partial class LogicConstructorPanel : UserControl

{

    private double _targetSplitRatio = WorkspaceLayoutNormalizer.DefaultConstructorSplit;

    private LogicProject _project = new();

    private readonly DockZoneManager _dockZones;

    private IReadOnlyList<ReferenceGraphPayload> _references = Array.Empty<ReferenceGraphPayload>();

    public event Action<string>? StatusChanged;

    public event Func<LogicProject, Task>? PreviewRequested;

    public event Action<LogicNode?, bool>? NodeSelectionChanged;

    public event Action<LogicEdge?>? EdgeSelectionChanged;

    public event Action? UserGraphChanged;

    public event Action<GameMemberNode>? MemberPreviewShown;

    public Func<(string typeName, string memberName)?>? RequestApiMember { get; set; }

    public IGameCodePreviewService? GameCodePreview { get; set; }

    public string? NuclearOptionRoot { get; set; }



    public LogicNodeCanvas UserGraphCanvas => UserCanvas;

    public LogicNodeCanvas ReferenceGraphCanvas => ReferenceCanvas;



    public Control SplitGrip => ConstructorSplit;



    public Grid SplitHost => SplitRoot;



    public LogicConstructorPanel()

    {

        InitializeComponent();

        _dockZones = new DockZoneManager(LogicProjectStore.LoadLayout());

        ConstructorSplit.DragCompleted += OnConstructorSplitDragCompleted;

        ReferenceCanvas.IsReadOnly = true;

        ReferenceCanvas.AllowReferenceDrop = true;

        UserCanvas.IsReadOnly = false;

        UserCanvas.AllowReferenceDrop = false;



        ReferenceCanvas.SelectionChanged += node =>

            NodeSelectionChanged?.Invoke(node, true);

        ReferenceCanvas.EdgeSelectionChanged += edge =>

            EdgeSelectionChanged?.Invoke(edge);

        ReferenceCanvas.StatusChanged += msg => StatusChanged?.Invoke($"[Ref] {msg}");

        ReferenceCanvas.ReferenceDrop += OnReferenceDrop;



        UserCanvas.SelectionChanged += node => NodeSelectionChanged?.Invoke(node, false);

        UserCanvas.EdgeSelectionChanged += edge => EdgeSelectionChanged?.Invoke(edge);

        UserCanvas.StatusChanged += msg => StatusChanged?.Invoke(msg);

        UserCanvas.GraphChanged += () =>
        {
            SyncProjectFromUi();
            UserGraphChanged?.Invoke();
        };



        Loaded += OnConstructorLoaded;

        LoadProject();

    }



    public async Task PreviewAsync()

    {

        SyncProjectFromUi();

        if (PreviewRequested != null)

        {

            await PreviewRequested(_project);

        }

    }



    public LogicProject Project

    {

        get

        {

            SyncProjectFromUi();

            return _project;

        }

    }



    public void ApplyInspector(LogicNode node, Dictionary<string, string> parameters)

    {

        UserCanvas.UpdateSelectedParameters(parameters);

        SyncProjectFromUi();

    }



    public void ApplyEdgeInspector(LogicEdge edge, string fromPort, string toPort)

    {

        UserCanvas.UpdateSelectedEdgePorts(fromPort, toPort, lockPorts: true);

        SyncProjectFromUi();

    }



    public void UpdateLiveTelemetry(BindingWatchPayload payload)

    {

        if (payload.telemetry.Count == 0)

        {

            return;

        }



        var parts = payload.telemetry.Select(kv => $"{kv.Key}={kv.Value:F1}");

        StatusChanged?.Invoke("Live: " + string.Join(" | ", parts));

    }



    private async Task ImportMethodPreviewFromDropAsync(GameMemberNode member)
    {
        var preview = GameCodePreview;
        if (preview != null)
        {
            var owner = ResolveOwnerType(member);
            var text = await preview.ResolvePreviewAsync(member, owner, NuclearOptionRoot).ConfigureAwait(true);
            var enriched = preview.WithPreview(member, text);
            ImportMethodPreviewGraph(enriched, fromDecompiler: true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(member.PreviewText))
        {
            ImportMethodPreviewGraph(member);
        }
        else
        {
            StatusChanged?.Invoke("[Ref] No preview; set game path in settings.");
        }
    }

    private async Task ImportTypePreviewFromDropAsync(GameTypeNode type, string? displayName)
    {
        var title = string.IsNullOrWhiteSpace(displayName)
            ? PlainLabelService.ForType(type.FullName)
            : displayName;
        var preview = GameCodePreview;
        if (preview != null)
        {
            var best = await preview.ResolveBestMethodForTypeAsync(type, NuclearOptionRoot).ConfigureAwait(true);
            if (best != null)
            {
                var graph = MethodPreviewGraphBuilder.Build(best);
                ApplyReferenceGraph(graph, $"type:{type.ShortName}", $"Type: {title}");
                StatusChanged?.Invoke(
                    $"[Ref] Type {type.ShortName} → {best.Name}: {graph.nodes.Length} block(s) (decompile from DLL if needed).");
                return;
            }
        }

        var fallback = TypePreviewGraphBuilder.Build(type);
        ApplyReferenceGraph(fallback, $"type:{type.ShortName}", $"Type: {title}");
        StatusChanged?.Invoke(
            $"[Ref] Type {type.ShortName}: {fallback.nodes.Length} block(s) on developer graph.");
    }

    private static GameTypeNode? ResolveOwnerType(GameMemberNode member)
    {
        var parts = member.BindingId.Split('.');
        if (parts.Length < 3)
        {
            return null;
        }

        var typeName = string.Join('.', parts.Skip(1).Take(parts.Length - 2));
        return new GameTypeNode
        {
            FullName = typeName,
            ShortName = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName,
            Parameters = Array.Empty<GameMemberNode>(),
            Methods = Array.Empty<GameMemberNode>(),
            Values = Array.Empty<GameMemberNode>()
        };
    }

    public void ImportMethodPreviewGraph(GameMemberNode member, bool fromDecompiler = false)

    {

        var graph = MethodPreviewGraphBuilder.Build(member);

        ApplyReferenceGraph(graph, referenceId: $"method:{member.Name}", title: $"Method: {member.Name}");

        MemberPreviewShown?.Invoke(member);

        var suffix = fromDecompiler ? " (decompile from DLL if needed)" : string.Empty;
        StatusChanged?.Invoke($"[Ref] Method {member.Name}: {graph.nodes.Length} block(s) on developer graph.{suffix}");

    }



    public void BindSelectedMember()

    {

        var member = RequestApiMember?.Invoke();

        if (member == null)

        {

            StatusChanged?.Invoke("Select a type/field in Game Code.");

            return;

        }



        var bindingId = $"Member.{member.Value.typeName}.{member.Value.memberName}";

        var parameters = new Dictionary<string, string> { ["bindingId"] = bindingId };
        if (GameCodeDragContext.LastDraggedMember != null)
        {
            GameBindingValueSchema.CopyBindingMetadata(parameters, GameCodeDragContext.LastDraggedMember);
        }

        UserCanvas.AddNodeFromPalette("source", "Member.Bind", 60, 60, parameters);

        SyncProjectFromUi();

    }



    private async void OnReferenceDrop(string kind, string typeId, string bindingId, string? displayName)
    {
        var draggedMember = GameCodeDragContext.LastDraggedMember;
        var draggedType = GameCodeDragContext.LastDraggedType;
        GameCodeDragContext.Clear();

        if (draggedMember != null)
        {
            if (draggedMember.Kind == GameMemberKind.Method)
            {
                await ImportMethodPreviewFromDropAsync(draggedMember).ConfigureAwait(true);
                return;
            }

            bindingId = draggedMember.BindingId;
            displayName ??= draggedMember.Name;
        }
        else if (draggedType != null)
        {
            await ImportTypePreviewFromDropAsync(draggedType, displayName).ConfigureAwait(true);
            return;
        }

        ReferenceGraphPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(bindingId))
        {
            payload = ReferenceDemoResolver.ResolveFromBinding(bindingId, _references);
        }

        if (payload == null && !string.IsNullOrWhiteSpace(typeId))
        {
            payload = ReferenceDemoResolver.ResolveFromPalette(typeId, _references);
        }

        if (payload == null)
        {
            StatusChanged?.Invoke("[Ref] Reference graph not found for this item.");
            return;
        }

        ApplyReferenceGraph(payload.graph, payload.id, payload.title);
        StatusChanged?.Invoke($"[Ref] Loaded example: {payload.title}");
    }



    private void ApplyReferenceGraph(LogicGraph graph, string? referenceId, string? title = null)

    {

        _project.referenceGraph = graph;

        if (!string.IsNullOrWhiteSpace(referenceId))

        {

            _project.referenceId = referenceId;
        }



        ReferenceCanvas.SetGraph(graph);

        if (!string.IsNullOrWhiteSpace(title))

        {

            StatusChanged?.Invoke($"[Ref] {title}");

        }

    }



    private void CopyReferenceToUser()

    {

        var graph = ReferenceCanvas.GetGraph();

        if (graph.nodes.Length == 0)

        {

            StatusChanged?.Invoke("Top graph is empty — pick an example or drag from Game Code.");

            return;

        }



        UserCanvas.DuplicateSubgraph(graph);

        StatusChanged?.Invoke($"Copied {graph.nodes.Length} block(s) to user constructor.");

        SyncProjectFromUi();

    }



    private void ApplyReferenceById(string referenceId)
    {
        var match = _references.FirstOrDefault(r =>
            string.Equals(r.id, referenceId, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            ApplyReferenceGraph(match.graph, match.id, match.title);
        }
    }



    private void OnConstructorLoaded(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(ApplySavedSplitRatio, DispatcherPriority.Loaded);

    private void OnConstructorSplitDragCompleted(object? sender, VectorEventArgs e) =>
        PersistConstructorSplitFromLayout();

    private void ApplySavedSplitRatio()
    {
        var fromProject = _project.layout?.splitRatio ?? double.NaN;
        var fromDock = _dockZones.Layout.splitRatio;
        var raw = !double.IsNaN(fromProject) ? fromProject : fromDock;
        var ratio = WorkspaceLayoutNormalizer.NormalizeConstructorSplit(raw);

        StudioUiInteractionTrace.Log("split.load",
            $"project={fromProject:F3} dock={fromDock:F3} raw={raw:F3} normalized={ratio:F3}");
        ApplyConstructorSplitRatio(ratio);

        if (WorkspaceLayoutNormalizer.IsLegacyConstructorSplit(raw))
        {
            PersistConstructorSplitFromLayout();
            _dockZones.Persist();
        }
    }

    public double ReadSplitRatio()
    {
        var top = SplitRoot.RowDefinitions[0].ActualHeight;
        var bottom = SplitRoot.RowDefinitions[2].ActualHeight;
        var sum = top + bottom;
        return sum > 1 ? top / sum : _targetSplitRatio;
    }

    public void ApplyConstructorSplitRatio(double ratio)
    {
        ratio = WorkspaceLayoutNormalizer.NormalizeConstructorSplit(ratio);
        _targetSplitRatio = ratio;
        StudioUiInteractionTrace.Log("split.apply", $"ApplyConstructorSplitRatio ratio={ratio:F3}");
        ConstructorSplitLayout.ApplyStarRows(SplitRoot, ratio);
    }



    private void PersistConstructorSplitFromLayout()

    {

        var ratio = ReadSplitRatio();

        _dockZones.SetSplitRatio(ratio);

        _dockZones.Persist();

        if (_project.layout != null)

        {

            _project.layout.splitRatio = ratio;

        }

        StudioUiInteractionTrace.Log("split.persist", $"saved ratio={ratio:F3}");
    }



    private void RefreshReferences() =>
        _references = LogicProjectStore.LoadAllReferences();



    private void SyncProjectFromUi()

    {

        _project.userGraph = UserCanvas.GetGraph();

        _project.referenceGraph = ReferenceCanvas.GetGraph();

        _project.layout = _dockZones.Layout;

    }



    public void SaveProject()

    {

        SyncProjectFromUi();

        LogicProjectStore.Save(_project);

        _dockZones.Persist();

        StatusChanged?.Invoke($"Saved: {LogicProjectStore.ProjectPath}");

    }



    public void LoadProject()

    {

        _project = LogicProjectStore.Load();

        RefreshReferences();

        if (_project.referenceGraph.nodes.Length == 0 && !string.IsNullOrWhiteSpace(_project.referenceId))

        {

            var loaded = LogicProjectStore.LoadAllReferences()

                .FirstOrDefault(r => string.Equals(r.id, _project.referenceId, StringComparison.OrdinalIgnoreCase));

            if (loaded != null)

            {

                _project.referenceGraph = loaded.graph;

            }

        }



        ReferenceCanvas.SetGraph(_project.referenceGraph);

        UserCanvas.SetGraph(_project.userGraph);



        if (!string.IsNullOrWhiteSpace(_project.referenceId))
        {
            ApplyReferenceById(_project.referenceId);
        }
        else if (_references.Count > 0)
        {
            var first = _references[0];
            ApplyReferenceGraph(first.graph, first.id, first.title);
        }



        ApplySavedSplitRatio();

        StatusChanged?.Invoke("Logic project loaded.");

    }



    public void SaveNosdk()

    {

        SyncProjectFromUi();

        var path = Path.Combine(LogicProjectStore.NosdkDir, $"{_project.name}.nosdk.json");

        LogicProjectStore.SaveNosdk(new NosdkProject

        {

            name = _project.name,

            logic = _project

        }, path);

        StatusChanged?.Invoke($"Saved .nosdk: {path}");

    }



    public void RequestBuildMod()
    {
        GameCodeIndexBootstrap.EnsureLoaded(NuclearOptionRoot);
        var source = LogicModSourceGenerator.Generate(Project);
        var request = LogicModExporter.CreateBuildRequest(
            "LogicMod",
            Guid.NewGuid().ToString(),
            Project,
            Path.Combine(LogicProjectStore.NosdkDir, "LogicMod"),
            source);
        ModProjectBuilder.Build(request, NuclearOptionRoot ?? string.Empty);
    }

}


