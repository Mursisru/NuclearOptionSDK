using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NuclearOptionSDK.Studio.Services;
using NuclearOptionSDK.Studio.Services.ApiSurface;
using NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;
using NuclearOptionSDK.Studio.Services.ApiSurface.Context;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Views;

public partial class GameCodePanel : UserControl
{
    public const string DragFormat = "NuclearOptionSDK.GameCode";
    public static readonly DataFormat<string> DragDataFormat =
        DataFormat.CreateStringApplicationFormat(DragFormat);

    private const double DragThreshold = 7;

    private IReadOnlyList<GameTypeNode> _types = Array.Empty<GameTypeNode>();
    private string? _gameRoot;
    private IGameCodePreviewService? _preview;
    private int _previewGeneration;
    private bool _dragActive;
    private GameMemberNode? _dragMember;
    private GameTypeNode? _dragType;
    private Point? _pressPoint;
    private PointerPressedEventArgs? _pressArgs;
    private readonly DebouncedAction _debouncedRebuild;

    public event Action<string, string>? PreviewSelectionChanged;

    public GameCodePanel()
    {
        InitializeComponent();
        _debouncedRebuild = new DebouncedAction(RebuildTree);
        RefreshButton.Click += (_, _) => LoadIndex();
        FilterBox.TextChanged += (_, _) => _debouncedRebuild.Trigger();
        PlainLanguageCheck.IsCheckedChanged += (_, _) => RebuildTree();
        HideSystemNoiseCheck.IsCheckedChanged += (_, _) => LoadIndex();
        ShowLifecycleCheck.IsCheckedChanged += (_, _) => LoadIndex();
        TypeTree.SelectionChanged += (_, _) => _ = UpdatePreviewAsync();
    }

    public void ShowMemberPreview(GameMemberNode member) =>
        PreviewSelectionChanged?.Invoke(member.Name, member.PreviewText ?? member.Signature);

    public void Configure(string? gameRoot, IGameCodePreviewService? preview = null)
    {
        _gameRoot = gameRoot;
        _preview = preview;
    }

    public void LoadIndex()
    {
        if (string.IsNullOrWhiteSpace(_gameRoot))
        {
            _types = Array.Empty<GameTypeNode>();
            GameCodeIndexCache.Clear();
            IndexStatusText.Text = "Game path not set.";
            RebuildTree();
            return;
        }

        try
        {
            var options = new GameCodeLoadOptions
            {
                HideSystemNoise = HideSystemNoiseCheck.IsChecked == true,
                ShowUnityLifecycle = ShowLifecycleCheck.IsChecked == true
            };
            _types = GameCodeIndexService.LoadFromGame(_gameRoot, filter: null, options);
            GameCodeIndexCache.SetIndex(_types);
            IndexStatusText.Text = $"{_types.Count} types (ApiSurface)";
        }
        catch (Exception ex)
        {
            _types = Array.Empty<GameTypeNode>();
            GameCodeIndexCache.Clear();
            IndexStatusText.Text = ex.Message;
        }

        RebuildTree();
    }

    private void RebuildTree()
    {
        var filter = FilterBox.Text?.Trim() ?? string.Empty;
        var usePlain = PlainLanguageCheck.IsChecked == true;
        var items = new List<TreeViewItem>();

        foreach (var type in _types)
        {
            var typeLabel = usePlain
                ? SymbolLabelService.ForType(type.FullName, type.DisplayTag)
                : type.ShortName;
            var typeScore = string.IsNullOrEmpty(filter)
                ? 0
                : Math.Max(FuzzySearchService.Score(type.FullName, filter), FuzzySearchService.Score(typeLabel, filter));

            var allMembers = type.AllMembers().ToList();
            var filteredMembers = FilterMembers(allMembers, filter, type.FullName, usePlain);
            var memberMatch = filteredMembers.Count > 0;

            if (!string.IsNullOrEmpty(filter) && typeScore < 0 && !memberMatch)
            {
                continue;
            }

            var root = new TreeViewItem { Header = typeLabel, Tag = type };
            ToolTip.SetTip(root, type.FullName);

            foreach (var bucket in new[]
                     {
                         MemberBehaviorBucket.Action,
                         MemberBehaviorBucket.Data,
                         MemberBehaviorBucket.Reference
                     })
            {
                var bucketMembers = filteredMembers
                    .Where(m => m.Behavior == bucket)
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (bucketMembers.Count == 0)
                {
                    continue;
                }

                var folderName = MemberBehaviorClassifier.FolderName(bucket);
                root.Items.Add(BuildGroup(folderName, bucketMembers, type.FullName, usePlain));
            }

            if (string.IsNullOrEmpty(filter) || typeScore >= 0 || memberMatch)
            {
                items.Add(root);
            }
        }

        TypeTree.ItemsSource = items;
    }

    private static List<GameMemberNode> FilterMembers(
        IReadOnlyList<GameMemberNode> members,
        string filter,
        string typeName,
        bool usePlain)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return members.OrderBy(m => m.Name).ToList();
        }

        var filterForActions = ActionMethodLabel.StripCallSuffix(filter);

        return members
            .Select(m =>
            {
                var label = FormatMemberLabel(m, typeName, usePlain);
                var searchLabel = m.Behavior == MemberBehaviorBucket.Action
                    ? ActionMethodLabel.StripCallSuffix(label)
                    : label;
                var score = Math.Max(
                    Math.Max(
                        FuzzySearchService.Score(m.Name, filter),
                        FuzzySearchService.Score(m.Name, filterForActions)),
                    Math.Max(
                        Math.Max(
                            FuzzySearchService.Score(searchLabel, filter),
                            FuzzySearchService.Score(searchLabel, filterForActions)),
                        Math.Max(
                            FuzzySearchService.Score(m.Hint ?? string.Empty, filter),
                            FuzzySearchService.Score(m.InspectorLine ?? string.Empty, filter))));
                return new { Member = m, Score = score, Label = label };
            })
            .Where(x => x.Score >= 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Member)
            .ToList();
    }

    private static TreeViewItem BuildGroup(
        string title,
        IReadOnlyList<GameMemberNode> members,
        string typeName,
        bool usePlain)
    {
        var group = new TreeViewItem { Header = $"{title} ({members.Count})" };
        foreach (var member in members)
        {
            group.Items.Add(CreateMemberItem(member, typeName, usePlain));
        }

        return group;
    }

    private static TreeViewItem CreateMemberItem(GameMemberNode member, string typeName, bool usePlain)
    {
        var label = FormatMemberLabel(member, typeName, usePlain);
        var hint = member.Hint ?? member.Signature;
        var tip = $"{member.InspectorLine}\n\n{hint}\n{member.BindingId}{member.CollisionBadge ?? string.Empty}";

        var item = new TreeViewItem { Header = label, Tag = member };
        ToolTip.SetTip(item, tip);
        return item;
    }

    private static string FormatMemberLabel(GameMemberNode member, string typeName, bool usePlain)
    {
        if (!usePlain)
        {
            return member.Name;
        }

        var title = member.ContextTitle
                    ?? SymbolLabelService.ForMember(typeName, member.Name, member.Signature);
        if (member.Behavior == MemberBehaviorBucket.Action)
        {
            title = ActionMethodLabel.StripCallSuffix(title);
        }

        return title;
    }

    private async Task UpdatePreviewAsync(bool bypassCache = false)
    {
        var generation = ++_previewGeneration;

        if (TypeTree.SelectedItem is TreeViewItem { Tag: GameMemberNode member })
        {
            var owner = FindOwnerType(member);
            var detail = member.InspectorLine + "\n\n" + member.Hint + "\n\n" + member.Signature;

            PreviewSelectionChanged?.Invoke(member.Name, member.PreviewText ?? detail);
            if (_preview != null)
            {
                var text = await _preview.ResolvePreviewAsync(member, owner, _gameRoot, bypassCache)
                    .ConfigureAwait(true);
                if (generation != _previewGeneration)
                {
                    return;
                }

                PreviewSelectionChanged?.Invoke(member.Name, text);
            }

            return;
        }

        if (TypeTree.SelectedItem is TreeViewItem { Tag: GameTypeNode type })
        {
            var header =
                $"// {type.FullName}\n// [{OwnerContextClassifier.OwnerLabel(type.OwnerContext)}] " +
                $"Data: {type.Parameters.Count}, Actions: {type.Methods.Count}, Reference: {type.Values.Count}";
            PreviewSelectionChanged?.Invoke(type.ShortName, header);

            if (_preview != null && type.Methods.Count > 0)
            {
                var best = await _preview.ResolveBestMethodForTypeAsync(type, _gameRoot, bypassCache)
                    .ConfigureAwait(true);
                if (generation != _previewGeneration)
                {
                    return;
                }

                if (best != null)
                {
                    PreviewSelectionChanged?.Invoke(
                        $"{type.ShortName}.{best.Name}",
                        best.PreviewText ?? best.Signature);
                }
            }

            return;
        }

        PreviewSelectionChanged?.Invoke(string.Empty, string.Empty);
    }

    private GameTypeNode? FindOwnerType(GameMemberNode member)
    {
        var parts = member.BindingId.Split('.');
        if (parts.Length < 3)
        {
            return null;
        }

        var typeName = string.Join('.', parts.Skip(1).Take(parts.Length - 2));
        return _types.FirstOrDefault(t =>
            string.Equals(t.FullName, typeName, StringComparison.Ordinal)
            || string.Equals(t.ShortName, typeName, StringComparison.Ordinal));
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TypeTree.AddHandler(InputElement.PointerPressedEvent, OnTreePointerPressed, handledEventsToo: true);
        TypeTree.AddHandler(InputElement.PointerMovedEvent, OnTreePointerMoved, handledEventsToo: true);
        TypeTree.AddHandler(InputElement.PointerReleasedEvent, OnTreePointerReleased, handledEventsToo: true);

        var refreshItem = new MenuItem { Header = "Refresh preview from DLL" };
        refreshItem.Click += async (_, _) => await UpdatePreviewAsync(bypassCache: true);
        var decompileItem = new MenuItem { Header = "Decompile from DLL" };
        decompileItem.Click += async (_, _) => await UpdatePreviewAsync(bypassCache: true);
        var radarItem = new MenuItem { Header = "Show dependency radar" };
        radarItem.Click += (_, _) => ShowDependencyRadarForSelection();
        TypeTree.ContextMenu = new ContextMenu { Items = { decompileItem, refreshItem, radarItem } };
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dragActive)
        {
            return;
        }

        _dragMember = null;
        _dragType = null;
        GameCodeDragContext.Clear();

        var (member, type) = ResolveDragTarget(e.Source);
        if (member != null)
        {
            _dragMember = member;
        }
        else if (type != null)
        {
            _dragType = type;
        }
        else
        {
            return;
        }

        _pressPoint = e.GetPosition(TypeTree);
        _pressArgs = e;
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragActive || (_dragMember == null && _dragType == null) || _pressPoint == null || _pressArgs == null)
        {
            return;
        }

        if (e.GetCurrentPoint(TypeTree).Properties.IsLeftButtonPressed != true)
        {
            return;
        }

        var pos = e.GetPosition(TypeTree);
        var dx = pos.X - _pressPoint.Value.X;
        var dy = pos.Y - _pressPoint.Value.Y;
        if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
        {
            return;
        }

        _dragActive = true;
        try
        {
            string payload;
            if (_dragMember != null)
            {
                var member = _dragMember;
                GameCodeDragContext.LastDraggedMember = member;
                GameCodeDragContext.LastDraggedType = null;
                var typeParts = member.BindingId.Split('.');
                var typeFullName = typeParts.Length >= 3
                    ? string.Join('.', typeParts.Skip(1).Take(typeParts.Length - 2))
                    : string.Empty;
                var displayName = PlainLanguageCheck.IsChecked == true
                    ? SymbolLabelService.ForMember(typeFullName, member.Name, member.Signature)
                    : member.Name;
                var readId = ApiSymbolIdFactory.TryResolveReadId(typeFullName, member.Name);
                var typeId = readId ?? "Member.Bind";
                payload = StudioDragPayload.Encode(
                    "source", typeId, member.BindingId, displayName, member.ClrTypeName);
            }
            else
            {
                var type = _dragType!;
                GameCodeDragContext.LastDraggedType = type;
                GameCodeDragContext.LastDraggedMember = null;
                var bindingId = $"Member.{type.FullName}";
                var label = PlainLanguageCheck.IsChecked == true
                    ? SymbolLabelService.ForType(type.FullName, type.DisplayTag)
                    : type.ShortName;
                payload = StudioDragPayload.Encode("source", "Member.Bind", bindingId, label);
            }

            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DragDataFormat, payload));
            await DragDrop.DoDragDropAsync(_pressArgs, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            StudioFileLogger.Error("gamecode-drag", ex.ToString());
        }
        finally
        {
            _dragActive = false;
            _dragMember = null;
            _dragType = null;
            _pressPoint = null;
            _pressArgs = null;
        }
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragMember = null;
        _dragType = null;
        _pressPoint = null;
        _pressArgs = null;
    }

    private static (GameMemberNode? member, GameTypeNode? type) ResolveDragTarget(object? source)
    {
        while (source is Control control)
        {
            if (control is TreeViewItem item)
            {
                if (item.Tag is GameMemberNode member)
                {
                    return (member, null);
                }

                if (item.Tag is GameTypeNode type)
                {
                    return (null, type);
                }
            }

            source = control.Parent;
        }

        return (null, null);
    }

    private void ShowDependencyRadarForSelection()
    {
        if (TypeTree.SelectedItem is not TreeViewItem { Tag: GameMemberNode member })
        {
            return;
        }

        var radar = GameCodeIndexCache.TryGetDependencyRadar(member.BindingId);
        if (radar == null)
        {
            PreviewSelectionChanged?.Invoke(
                $"Dependency Radar: {member.Name}",
                $"// No dependency data for {member.BindingId}");
            return;
        }

        var lines = new List<string>
        {
            $"// Dependency radar for {radar.bindingId}",
            "// Writers:"
        };
        lines.AddRange(radar.writers.Select(w => $"// - {w.typeName}.{w.methodName} ({w.usage})"));
        lines.Add("// Readers:");
        lines.AddRange(radar.readers.Select(r => $"// - {r.typeName}.{r.methodName} ({r.usage})"));
        if (radar.warnings.Length > 0)
        {
            lines.Add("// Warnings:");
            lines.AddRange(radar.warnings.Select(w => $"// - {w}"));
        }

        PreviewSelectionChanged?.Invoke($"Dependency Radar: {member.Name}", string.Join(Environment.NewLine, lines));
    }

    public (string typeName, string memberName)? GetSelectedMemberBinding()
    {
        if (TypeTree.SelectedItem is not TreeViewItem { Tag: GameMemberNode member })
        {
            return null;
        }

        var parts = member.BindingId.Split('.');
        if (parts.Length < 3)
        {
            return null;
        }

        var memberName = parts[^1];
        var typeName = string.Join('.', parts.Skip(1).Take(parts.Length - 2));
        return (typeName, memberName);
    }
}
