using Newtonsoft.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using NuclearOptionSDK.Decompiler;
using NuclearOptionSDK.ModKit;
using NuclearOptionSDK.Protocol;
using NuclearOptionSDK.Studio.Services;
using NuclearOptionSDK.Studio.Views;

namespace NuclearOptionSDK.Studio;

public partial class MainWindow : Window
{
    public Grid WorkspaceHost => WorkspaceGrid;
    public ConstructorWorkspacePanel ConstructorHost => ConstructorWorkspace;
    public TabControl CenterTabs => CenterEditorTabs;

    public int CenterEditorSelectedIndex
    {
        get => _centerEditorIndex;
        set => SetCenterEditorTab(value);
    }

    private int _centerEditorIndex = -1;

    private readonly BridgeClient _client = new();
    private AppSettings _settings = new();
    private readonly IGameCodePreviewService _gameCodePreview = new GameCodePreviewService();
    private int? _selectedHudInstanceId;
    private HudElementNode? _selectedHudNode;
    private GameObjectNode? _selectedSceneNode;
    private VisualHudSelection? _visualSelection;
    private LogicNode? _selectedLogicNode;
    private LogicEdge? _selectedLogicEdge;
    private string _inspectorMode = "none";
    private bool _suppressInspectorEvents;
    private bool _logicInspectorReadOnly;
    private readonly LogicInspectorBuilder _logicInspector;

    public MainWindow()
    {
        InitializeComponent();

        CenterEditorTabs.SelectionChanged += (_, _) =>
            SetCenterEditorTab(CenterEditorTabs.SelectedIndex);

        _logicInspector = new LogicInspectorBuilder(LogicParamsPanel);
        _logicInspector.ParametersEdited += ApplyInspectorToLogicNodeLive;
        Title = $"Nuclear Studio — {AppVersion.Display}";
        ServicesVersionText.Text = AppVersion.Display;
        _settings = AppSettingsStore.Load();
        var installCheck = GameInstallValidator.Validate(_settings.NuclearOptionRoot);
        if (!installCheck.IsValid && !GameInstallValidator.BypassValidation)
        {
            throw new InvalidOperationException(installCheck.Message);
        }

        GamePathBox.Text = _settings.NuclearOptionRoot;
        ReplDisclaimerCheck.IsChecked = _settings.ReplDisclaimerAccepted;

        _client.ConnectionStateChanged += state => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ConnectionStatusText.Text = state;
            StatusBarText.Text = state;
            ConnectionIndicator.Background = state == "Connected"
                ? new SolidColorBrush(Color.Parse("#33AA55"))
                : new SolidColorBrush(Color.Parse("#CC3333"));
        });
        _client.Log += msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendLog(msg));
        _client.MessageReceived += envelope => Avalonia.Threading.Dispatcher.UIThread.Post(() => HandlePush(envelope));

        MenuShowPreview.Click += (_, _) =>
        {
            if (MenuShowPreview.IsChecked == true)
            {
                BottomDockTabs.SelectedItem = PreviewDockTab;
            }
            else if (BottomDockTabs.SelectedItem == PreviewDockTab)
            {
                BottomDockTabs.SelectedItem = ProjectDockTab;
            }
        };
        MenuShowConsole.Click += (_, _) =>
        {
            ConsoleDockTab.IsVisible = MenuShowConsole.IsChecked == true;
            if (MenuShowConsole.IsChecked == true)
            {
                BottomDockTabs.SelectedItem = ConsoleDockTab;
            }
        };
        MenuToggleTools.Click += (_, _) =>
            ReplDockTab.IsVisible = MenuToggleTools.IsChecked == true;

        MenuConnect.Click += async (_, _) => await ConnectAsync();
        MenuDisconnect.Click += async (_, _) => await _client.DisconnectAsync();
        MenuPing.Click += async (_, _) => await PingAsync();
        MenuSaveSettings.Click += (_, _) => SaveSettings();
        MenuSaveProject.Click += (_, _) => ConstructorWorkspace.Logic.SaveProject();
        MenuLoadProject.Click += (_, _) => ConstructorWorkspace.Logic.LoadProject();
        MenuSaveNosdk.Click += (_, _) => ConstructorWorkspace.Logic.SaveNosdk();
        MenuExit.Click += (_, _) => Close();
        MenuPreviewHud.Click += async (_, _) => await PushVisualHudPreviewAsync();
        MenuPreviewLogic.Click += async (_, _) => await ConstructorWorkspace.Logic.PreviewAsync();
        MenuBuildHudMod.Click += async (_, _) => await BuildVisualModAsync(showOverlay: true);
        MenuBuildLogicMod.Click += (_, _) =>
        {
            ConstructorWorkspace.Logic.RequestBuildMod();
            ShowModBuildOverlay();
        };
        MenuOpenSettings.Click += async (_, _) => await OpenSettingsAsync();
        MenuFocusConstructor.Click += (_, _) => SetCenterEditorTab(0);
        MenuFocusVisualHud.Click += (_, _) => SetCenterEditorTab(1);
        MenuFocusScene.Click += (_, _) =>
        {
            SetCenterEditorTab(0);
            Hierarchy.SelectTab(HierarchyTab.Scene);
        };
        MenuFocusProject.Click += (_, _) => BottomDockTabs.SelectedItem = ProjectDockTab;
        MenuRefreshScene.Click += async (_, _) => await RefreshSceneAsync();
        ToolbarPreviewLogic.Click += async (_, _) => await ConstructorWorkspace.Logic.PreviewAsync();
        ToolbarPreviewHud.Click += async (_, _) => await PushVisualHudPreviewAsync();

        RunReplButton.Click += async (_, _) => await RunReplAsync();
        ClearLogButton.Click += (_, _) => LogBox.Text = string.Empty;
        InspectorApplyButton.Click += async (_, _) => await ApplyInspectorAsync();
        OverlayEnabledCheck.IsCheckedChanged += async (_, _) => await SetOverlayAsync();
        ModBuilder.GenerateHarmonyBtn.Click += (_, _) => GenerateHarmony();
        ModBuilder.BuildModBtn.Click += async (_, _) => await BuildModAsync();

        Hierarchy.RefreshSceneButton.Click += async (_, _) => await RefreshSceneAsync();
        Hierarchy.RefreshHudButton.Click += async (_, _) => await RefreshHudAsync();
        Hierarchy.SceneTree.SelectionChanged += (_, _) => OnSceneSelected();
        Hierarchy.HudTree.SelectionChanged += (_, _) => OnHudSelected();

        VisualHudEditor.StatusChanged += AppendLog;
        VisualHudEditor.PreviewRequested += PushVisualHudPreviewAsync;
        VisualHudEditor.SelectionChanged += OnVisualHudSelectionChanged;

        var logic = ConstructorWorkspace.Logic;
        logic.GameCodePreview = _gameCodePreview;
        logic.NuclearOptionRoot = _settings.NuclearOptionRoot;
        logic.StatusChanged += msg => { AppendLog(msg); StatusBarText.Text = msg; };
        logic.PreviewRequested += PushLogicPreviewAsync;
        logic.NodeSelectionChanged += (node, readOnly) => OnLogicNodeSelected(node, readOnly);
        logic.EdgeSelectionChanged += OnLogicEdgeSelected;
        logic.UserGraphChanged += () => RefreshLogicModPreview(force: true);
        logic.RequestApiMember = GetSelectedApiMember;
        logic.MemberPreviewShown += member =>
            ShowBottomPreview(member.Name, member.PreviewText ?? member.Signature);

        InnovationRegistry.RegisterDefaults();

        var gameCode = Hierarchy.GameCode;
        gameCode.Configure(_settings.NuclearOptionRoot, _gameCodePreview);
        gameCode.LoadIndex();
        gameCode.PreviewSelectionChanged += OnGameCodePreviewSelectionChanged;
        SceneHierarchyDrag.Enable(Hierarchy.SceneTree);

        var audio = ConstructorWorkspace.Audio;
        var mechanic = ConstructorWorkspace.Mechanic;
        var qol = ConstructorWorkspace.Qol;
        audio.Configure();
        mechanic.Configure("Mechanics — parameters → change", "mechanic-project");
        qol.Configure("QOL — improvements", "qol-project");
        audio.StatusChanged += AppendLog;
        mechanic.StatusChanged += AppendLog;
        qol.StatusChanged += AppendLog;
        audio.PreviewRequested += PushGraphPreviewAsync;
        mechanic.PreviewRequested += PushGraphPreviewAsync;
        qol.PreviewRequested += PushGraphPreviewAsync;

        ConstructorWorkspace.SelectedSectionChanged += (_, _) =>
        {
            if (_centerEditorIndex == 0)
            {
                OnCenterTabChanged();
            }
        };

        WireInspectorFields();
        ProtocolLive.TraceStartRequested += StartTraceAsync;
        ProtocolLive.TraceStopRequested += StopTraceAsync;

        LogPathText.Text = BuildLogPathLabel();
        StudioFileLogger.Info("startup", "Nuclear Studio opened.");
#if STUDIO_UI_TRACE
        if (StudioUiInteractionTrace.IsEnabled)
        {
            StudioFileLogger.Info("ui-trace", $"Interaction log: {StudioUiInteractionTrace.LogPath}");
        }
#endif

        SetCenterEditorTab(0);
        ApplyWorkspaceLayout();

        KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                await RunReplAsync();
            }
        };
    }

    private void ShowModBuildOverlay() => ModBuildOverlay.IsVisible = true;

    private async Task OpenSettingsAsync()
    {
        var dlg = new SettingsWindow();
        dlg.Load(_settings);
        if (await dlg.ShowDialog<bool>(this))
        {
            _settings = dlg.Result;
            var validation = GameInstallValidator.Validate(_settings.NuclearOptionRoot);
            if (!validation.IsValid)
            {
                AppSettingsStore.Save(_settings);
                if (Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = new GameRequiredWindow();
                    desktop.MainWindow.Show();
                    Close();
                }

                return;
            }

            GamePathBox.Text = _settings.NuclearOptionRoot;
            ReplDisclaimerCheck.IsChecked = _settings.ReplDisclaimerAccepted;
            AppSettingsStore.Save(_settings);
            var gameCode = Hierarchy.GameCode;
            gameCode.Configure(_settings.NuclearOptionRoot, _gameCodePreview);
            gameCode.LoadIndex();
            ConstructorWorkspace.Logic.NuclearOptionRoot = _settings.NuclearOptionRoot;
            AppendLog("Settings saved.");
        }
    }

    private void WireInspectorFields()
    {
        InspectorTextBox.TextChanged += (_, _) => OnInspectorFieldChanged();
        InspectorColorBox.TextChanged += (_, _) => OnInspectorFieldChanged();
        InspectorFontSizeBox.TextChanged += (_, _) => OnInspectorFieldChanged();
        InspectorXBox.TextChanged += (_, _) => OnInspectorFieldChanged();
        InspectorYBox.TextChanged += (_, _) => OnInspectorFieldChanged();
        InspectorX2Box.TextChanged += (_, _) => OnInspectorFieldChanged();
        InspectorY2Box.TextChanged += (_, _) => OnInspectorFieldChanged();
        InspectorRadiusBox.TextChanged += (_, _) => OnInspectorFieldChanged();
        InspectorVisibleCheck.IsCheckedChanged += (_, _) => OnInspectorFieldChanged();
    }

    private void OnVisualHudSelectionChanged(VisualHudSelection? selection)
    {
        _visualSelection = selection;
        if (selection != null)
        {
            Hierarchy.SceneTree.SelectedItem = null;
            Hierarchy.HudTree.SelectedItem = null;
            _selectedHudInstanceId = null;
            _selectedHudNode = null;
            _selectedSceneNode = null;
            ShowVisualInspector(selection);
        }
    }

    private void ShowVisualInspector(VisualHudSelection selection)
    {
        _inspectorMode = selection.Kind;
        HideInspectorPreviewOnly();
        _suppressInspectorEvents = true;
        InspectorTargetText.Text = selection.Kind switch
        {
            "label" => $"Visual Label ({selection.Id})",
            "line" => $"Visual Line ({selection.Id})",
            "circle" => $"Visual Circle ({selection.Id})",
            _ => "Visual element"
        };
        InspectorPathText.IsVisible = false;
        InspectorReadOnlyBox.IsVisible = false;
        InspectorTextBox.IsVisible = selection.Kind == "label";
        InspectorFontSizeBox.IsVisible = selection.Kind == "label";
        InspectorPositionPanel.IsVisible = true;
        InspectorLinePanel.IsVisible = selection.Kind == "line";
        InspectorRadiusBox.IsVisible = selection.Kind == "circle";
        InspectorVisibleCheck.IsVisible = selection.Kind == "label";
        InspectorColorBox.IsVisible = true;
        InspectorAutoApplyCheck.IsVisible = false;
        InspectorApplyButton.IsVisible = true;

        InspectorTextBox.Text = selection.Text ?? string.Empty;
        InspectorColorBox.Text = selection.ColorHtml;
        InspectorFontSizeBox.Text = selection.FontSize.ToString("0");
        InspectorXBox.Text = selection.X.ToString("0");
        InspectorYBox.Text = selection.Y.ToString("0");
        InspectorX2Box.Text = selection.X2.ToString("0");
        InspectorY2Box.Text = selection.Y2.ToString("0");
        InspectorRadiusBox.Text = selection.Radius.ToString("0");
        InspectorVisibleCheck.IsChecked = selection.Visible;
        _suppressInspectorEvents = false;
    }

    private async void OnInspectorFieldChanged()
    {
        if (_suppressInspectorEvents)
        {
            return;
        }

        if (_inspectorMode is "label" or "line" or "circle")
        {
            ApplyInspectorToVisualEditor();
            if (_client.IsConnected && InspectorAutoApplyCheck.IsChecked == true)
            {
                await PushVisualHudPreviewAsync();
            }

            return;
        }

        if (_inspectorMode == "logic")
        {
            return;
        }

        if (_inspectorMode == "hud" && InspectorAutoApplyCheck.IsChecked == true)
        {
            await ApplyHudAsync();
        }
    }

    private void ApplyInspectorToVisualEditor()
    {
        if (_visualSelection == null)
        {
            return;
        }

        var values = new VisualHudSelection
        {
            Kind = _visualSelection.Kind,
            Id = _visualSelection.Id,
            Text = InspectorTextBox.Text,
            ColorHtml = string.IsNullOrWhiteSpace(InspectorColorBox.Text) ? "#FFFFFF" : InspectorColorBox.Text.Trim(),
            Visible = InspectorVisibleCheck.IsChecked == true
        };

        if (double.TryParse(InspectorFontSizeBox.Text, out var fontSize))
        {
            values.FontSize = fontSize;
        }

        if (double.TryParse(InspectorXBox.Text, out var x))
        {
            values.X = x;
        }

        if (double.TryParse(InspectorYBox.Text, out var y))
        {
            values.Y = y;
        }

        if (double.TryParse(InspectorX2Box.Text, out var x2))
        {
            values.X2 = x2;
        }

        if (double.TryParse(InspectorY2Box.Text, out var y2))
        {
            values.Y2 = y2;
        }

        if (double.TryParse(InspectorRadiusBox.Text, out var radius))
        {
            values.Radius = radius;
        }

        VisualHudEditor.ApplyInspectorValues(values);
        _visualSelection = values;
    }

    private async Task ApplyInspectorAsync()
    {
        if (_inspectorMode == "hud")
        {
            await ApplyHudAsync();
            return;
        }

        if (_inspectorMode is "label" or "line" or "circle")
        {
            ApplyInspectorToVisualEditor();
            await PushVisualHudPreviewAsync();
            return;
        }

        if (_inspectorMode == "logic")
        {
            ApplyInspectorToLogicNode();
            return;
        }

        if (_inspectorMode == "logic-edge")
        {
            ApplyInspectorToLogicEdge();
            return;
        }
    }

    private void ResetInspectorEmpty()
    {
        _inspectorMode = "none";
        _selectedLogicNode = null;
        _selectedLogicEdge = null;
        _suppressInspectorEvents = true;
        InspectorTargetText.Text = "Nothing selected";
        InspectorPathText.IsVisible = false;
        InspectorLogicSummaryText.IsVisible = false;
        LogicParamsPanel.IsVisible = false;
        InspectorTextBox.IsVisible = false;
        InspectorColorBox.IsVisible = false;
        InspectorFontSizeBox.IsVisible = false;
        InspectorPositionPanel.IsVisible = false;
        InspectorLinePanel.IsVisible = false;
        InspectorRadiusBox.IsVisible = false;
        InspectorVisibleCheck.IsVisible = false;
        InspectorAutoApplyCheck.IsVisible = true;
        InspectorApplyButton.IsVisible = true;
        InspectorReadOnlyBox.IsVisible = false;
        _suppressInspectorEvents = false;
    }

    private void OnLogicEdgeSelected(LogicEdge? edge)
    {
        _selectedLogicEdge = edge;
        if (edge == null)
        {
            if (_inspectorMode is "logic" or "logic-edge")
            {
                ResetInspectorEmpty();
            }

            return;
        }

        _selectedLogicNode = null;
        _visualSelection = null;
        _inspectorMode = "logic-edge";
        HideInspectorPreviewOnly();
        LogicParamsPanel.IsVisible = false;
        InspectorLogicSummaryText.IsVisible = false;
        _suppressInspectorEvents = true;
        InspectorTargetText.Text = $"Edge: {edge.fromNode} → {edge.toNode}";
        InspectorReadOnlyBox.IsVisible = true;
        InspectorReadOnlyBox.Text = "Ports: out/in/right/left/top/bottom";
        InspectorTextBox.IsVisible = true;
        InspectorTextBox.PlaceholderText = "fromPort (out/right/top/bottom)";
        InspectorTextBox.Text = edge.fromPort;
        InspectorFontSizeBox.IsVisible = true;
        InspectorFontSizeBox.PlaceholderText = "toPort (in/left/top/bottom)";
        InspectorFontSizeBox.Text = edge.toPort;
        InspectorColorBox.IsVisible = false;
        InspectorVisibleCheck.IsVisible = false;
        InspectorPositionPanel.IsVisible = false;
        InspectorLinePanel.IsVisible = false;
        InspectorRadiusBox.IsVisible = false;
        InspectorApplyButton.IsVisible = true;
        _suppressInspectorEvents = false;
    }

    private void ApplyInspectorToLogicEdge()
    {
        if (_selectedLogicEdge == null)
        {
            return;
        }

        var fromPort = string.IsNullOrWhiteSpace(InspectorTextBox.Text) ? "out" : InspectorTextBox.Text.Trim();
        var toPort = string.IsNullOrWhiteSpace(InspectorFontSizeBox.Text) ? "in" : InspectorFontSizeBox.Text.Trim();
        ConstructorWorkspace.Logic.ApplyEdgeInspector(_selectedLogicEdge, fromPort, toPort);
    }

    private void OnLogicNodeSelected(LogicNode? node, bool readOnly)
    {
        _selectedLogicNode = node;
        _selectedLogicEdge = null;
        _logicInspectorReadOnly = readOnly;
        if (node == null)
        {
            if (_inspectorMode is "logic" or "logic-edge")
            {
                ResetInspectorEmpty();
            }

            return;
        }

        _visualSelection = null;
        _inspectorMode = "logic";
        _suppressInspectorEvents = true;

        var display = new DisplayLayerService();
        InspectorTargetText.Text = display.Title(node.typeId);
        InspectorPathText.IsVisible = false;

        var summary = _logicInspector.BuildSummaryText(node);
        InspectorLogicSummaryText.IsVisible = !string.IsNullOrWhiteSpace(summary);
        InspectorLogicSummaryText.Text = summary;

        InspectorReadOnlyBox.IsVisible = true;
        InspectorReadOnlyBox.Text = node.parameters.TryGetValue("steps", out var steps) && !string.IsNullOrWhiteSpace(steps)
            ? steps
            : _logicInspector.BuildHintText(node);

        LogicParamsPanel.IsVisible = true;
        _logicInspector.Bind(node, readOnly, ConstructorWorkspace.Logic.Project.userGraph);

        InspectorTextBox.IsVisible = false;
        InspectorColorBox.IsVisible = false;
        InspectorFontSizeBox.IsVisible = false;
        InspectorPositionPanel.IsVisible = false;
        InspectorLinePanel.IsVisible = false;
        InspectorRadiusBox.IsVisible = false;
        InspectorVisibleCheck.IsVisible = false;
        InspectorAutoApplyCheck.IsVisible = false;
        InspectorApplyButton.IsVisible = false;
        _suppressInspectorEvents = false;

        try
        {
            RefreshLogicModPreview();
        }
        catch (Exception ex)
        {
            AppendLog($"Preview code: {ex.Message}");
        }
    }

    private void RefreshLogicModPreview(bool force = false)
    {
        if (!force && !ShouldRefreshLogicModPreview())
        {
            return;
        }

        var project = ConstructorWorkspace.Logic.Project;
        ShowLogicCodePreview(LogicOutputPreviewBuilder.BuildGraphModPreview(project));
    }

    private bool ShouldRefreshLogicModPreview()
    {
        if (_inspectorMode == "logic")
        {
            return true;
        }

        return BottomDockTabs.SelectedItem == PreviewDockTab
               && BottomPreviewTitle.Text?.Contains("Generated mod source", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void ApplyInspectorToLogicNodeLive()
    {
        if (_selectedLogicNode == null || _logicInspectorReadOnly)
        {
            return;
        }

        var parameters = _logicInspector.ReadParameters();
        ConstructorWorkspace.Logic.ApplyInspector(_selectedLogicNode, parameters);

        var summary = _logicInspector.BuildSummaryText(_selectedLogicNode);
        InspectorLogicSummaryText.IsVisible = !string.IsNullOrWhiteSpace(summary);
        InspectorLogicSummaryText.Text = summary;
        RefreshLogicModPreview();
    }

    private void ApplyInspectorToLogicNode()
    {
        ApplyInspectorToLogicNodeLive();
    }

    private (string typeName, string memberName)? GetSelectedApiMember() =>
        Hierarchy.GameCode.GetSelectedMemberBinding();

    private void OnGameCodePreviewSelectionChanged(string title, string text)
    {
        if (Hierarchy.ContentTabs.SelectedIndex != (int)HierarchyTab.Code)
        {
            return;
        }

        ShowBottomPreview(title, text);
    }

    private void ShowBottomPreview(string title, string text)
    {
        BottomPreviewTitle.Text = string.IsNullOrWhiteSpace(title) ? "Preview" : title;
        BottomPreviewBox.Text = string.IsNullOrWhiteSpace(text) ? "// Select an item on the left" : text;
        MenuShowPreview.IsChecked = true;
        BottomDockTabs.SelectedItem = PreviewDockTab;
    }

    private void ShowLogicCodePreview(string text)
    {
        ShowBottomPreview("Generated mod source (full)", string.IsNullOrWhiteSpace(text)
            ? "// Build Source → Check → Output on the graph"
            : text);
    }

    private void SetCenterEditorTab(int index)
    {
        index = Math.Clamp(index, 0, 2);
        if (_centerEditorIndex == index)
        {
            return;
        }

        _centerEditorIndex = index;
        ConstructorWorkspace.IsVisible = index == 0;
        VisualHudEditor.IsVisible = index == 1;
        ProtocolLive.IsVisible = index == 2;

        if (CenterEditorTabs.SelectedIndex != index)
        {
            CenterEditorTabs.SelectedIndex = index;
        }

        OnCenterTabChanged();
    }

    private void OnCenterTabChanged()
    {
        switch (_centerEditorIndex)
        {
            case 0:
                BottomDockTabs.SelectedItem = ProjectDockTab;
                var section = ConstructorWorkspace.SelectedSectionIndex switch
                {
                    1 => "audio",
                    2 => "mechanic",
                    3 => "qol",
                    _ => "*"
                };
                Hierarchy.LogicPalette.SetCategoryFilter(section);
                break;
            case 2:
                BottomDockTabs.SelectedItem = ProjectDockTab;
                break;
        }
    }

    private void ApplyWorkspaceLayout()
    {
        var layout = LogicProjectStore.LoadLayout();
        var top = layout.workspaceTopRowWeight;
        var bottom = layout.workspaceBottomRowWeight;
        WorkspaceLayoutNormalizer.NormalizeWorkspaceRows(ref top, ref bottom);
        WorkspaceGrid.RowDefinitions[0] = new RowDefinition(new GridLength(top, GridUnitType.Star));
        WorkspaceGrid.RowDefinitions[2] = new RowDefinition(new GridLength(bottom, GridUnitType.Star));
    }

    private void OnWorkspaceRowSplitDragCompleted(object? sender, VectorEventArgs e)
    {
        var top = WorkspaceGrid.RowDefinitions[0].ActualHeight;
        var bottom = WorkspaceGrid.RowDefinitions[2].ActualHeight;
        if (top + bottom < 8)
        {
            return;
        }

        var layout = LogicProjectStore.LoadLayout();
        layout.workspaceTopRowWeight = top;
        layout.workspaceBottomRowWeight = bottom;
        LogicProjectStore.SaveLayout(layout);
    }

    private static void HideInspectorPreviewOnly()
    {
    }

    private async Task PushGraphPreviewAsync(LogicGraph graph)
    {
        var project = new LogicProject
        {
            name = "PreviewGraph",
            userGraph = graph
        };
        await PushLogicPreviewAsync(project);
    }

    private async Task PushLogicPreviewAsync(LogicProject project)
    {
        if (!_client.IsConnected)
        {
            AppendLog("Connect to Bridge before logic preview.");
            return;
        }

        OverlayEnabledCheck.IsChecked = true;
        await _client.SendFireAndForgetAsync(ProtocolJson.Create(
            MessageTypes.OverlaySetEnabled,
            new OverlayEnabledRequest { enabled = true }));

        var hudLayout = VisualHudEditor.BuildLayoutPayload();
        await _client.SendFireAndForgetAsync(ProtocolJson.Create(MessageTypes.OverlayLayout, hudLayout));

        var response = await _client.SendAsync(
            ProtocolJson.Create(MessageTypes.LogicSet, new LogicSetRequest { project = project, previewEnabled = true }),
            TimeSpan.FromSeconds(5));

        if (response?.type == MessageTypes.LogicStatus)
        {
            var status = ProtocolJson.Payload<LogicStatusPayload>(response);
            AppendLog($"Logic preview ON — {status.lastActions.Length} action(s).");
        }
        else
        {
            AppendLog("Logic preview sent.");
        }
    }

    private void SaveSettings()
    {
        _settings.NuclearOptionRoot = GamePathBox.Text?.Trim() ?? _settings.NuclearOptionRoot;
        _settings.ReplDisclaimerAccepted = ReplDisclaimerCheck.IsChecked == true;
        AppSettingsStore.Save(_settings);
        AppendLog("Settings saved.");
    }

    private async Task ConnectAsync()
    {
        SaveSettings();
        StudioFileLogger.Info("connect", "attempt");
        try
        {
            await _client.ConnectAsync("127.0.0.1", _settings.BridgePort);
            await RefreshBindingListAsync();
        }
        catch (Exception ex)
        {
            StudioFileLogger.Error("connect", ex.ToString());
            AppendLog($"Connect failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                AppendLog($"  → {ex.InnerException.Message}");
            }

            AppendLog("Hint: start NO with BepInEx, check BepInEx\\LogOutput.log for Bridge errors.");
        }
    }

    private async Task RefreshBindingListAsync()
    {
        if (!_client.IsConnected)
        {
            return;
        }

        var response = await _client.SendAsync(ProtocolJson.Create(MessageTypes.BindingList), TimeSpan.FromSeconds(5));
        if (response?.type == MessageTypes.BindingList)
        {
            var list = ProtocolJson.Payload<BindingListPayload>(response);
            ProtocolLive.SetJsonPreview(JsonConvert.SerializeObject(list, Formatting.Indented));
            AppendLog($"Bindings: {list.bindings.Length} curated.");
        }
    }

    private async Task PingAsync()
    {
        if (!_client.IsConnected)
        {
            AppendLog("Not connected.");
            return;
        }

        try
        {
            var request = ProtocolJson.Create(MessageTypes.Ping);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _client.SendAsync(request, TimeSpan.FromSeconds(5));
            sw.Stop();
            if (response?.type == MessageTypes.Pong)
            {
                var pong = ProtocolJson.Payload<PongPayload>(response);
                AppendLog($"Pong {sw.ElapsedMilliseconds} ms — Bridge {pong.bridgeVersion}, Unity {pong.gameVersion}");
            }
            else
            {
                AppendLog("Unexpected ping response.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Ping failed: {ex.Message}");
        }
    }

    private async Task RefreshSceneAsync()
    {
        if (!_client.IsConnected)
        {
            AppendLog("Not connected.");
            return;
        }

        var response = await _client.SendAsync(ProtocolJson.Create(MessageTypes.SceneGetRoots), TimeSpan.FromSeconds(10));
        if (response == null || response.type != MessageTypes.SceneTree)
        {
            AppendLog("Scene refresh failed.");
            return;
        }

        var tree = ProtocolJson.Payload<SceneTreePayload>(response);
        Hierarchy.SceneTree.Items.Clear();
        foreach (var root in tree.roots)
        {
            Hierarchy.SceneTree.Items.Add(BuildSceneNode(root));
        }

        AppendLog($"Scene '{tree.sceneName}' ({tree.roots.Count} roots).");
    }

    private TreeViewItem BuildSceneNode(GameObjectNode node)
    {
        var item = new TreeViewItem
        {
            Header = node.name,
            Tag = node
        };

        foreach (var child in node.children)
        {
            item.Items.Add(BuildSceneNode(child));
        }

        return item;
    }

    private async void OnSceneSelected()
    {
        if (Hierarchy.SceneTree.SelectedItem is not TreeViewItem item || item.Tag is not GameObjectNode node)
        {
            return;
        }

        _selectedSceneNode = node;
        _selectedHudInstanceId = null;
        _selectedHudNode = null;
        _visualSelection = null;
        _inspectorMode = "scene";
        _suppressInspectorEvents = true;

        InspectorTargetText.Text = node.name;
        InspectorPathText.Text = $"ID {node.id}";
        InspectorPathText.IsVisible = true;
        InspectorTextBox.IsVisible = false;
        InspectorFontSizeBox.IsVisible = false;
        InspectorPositionPanel.IsVisible = false;
        InspectorLinePanel.IsVisible = false;
        InspectorRadiusBox.IsVisible = false;
        InspectorVisibleCheck.IsVisible = false;
        InspectorColorBox.IsVisible = false;
        InspectorAutoApplyCheck.IsVisible = false;
        InspectorApplyButton.IsVisible = false;
        InspectorReadOnlyBox.IsVisible = true;
        InspectorReadOnlyBox.Text = string.Join(Environment.NewLine, node.components);

        if (_client.IsConnected)
        {
            var response = await _client.SendAsync(
                ProtocolJson.Create(MessageTypes.SceneResolve, new SceneResolveRequest { instanceId = node.id }),
                TimeSpan.FromSeconds(5));
            if (response?.type == MessageTypes.SceneResolved)
            {
                var resolved = ProtocolJson.Payload<SceneResolveResponse>(response);
                InspectorReadOnlyBox.Text = string.Join(Environment.NewLine, resolved.components);
            }
        }

        _suppressInspectorEvents = false;
    }

    private async Task RunReplAsync()
    {
        if (!_client.IsConnected)
        {
            AppendLog("Not connected.");
            return;
        }

        if (ReplDisclaimerCheck.IsChecked != true)
        {
            ReplResultText.Text = "Accept disclaimer first.";
            return;
        }

        _settings.ReplDisclaimerAccepted = true;
        AppSettingsStore.Save(_settings);

        var friendly = ReplInput.Text ?? string.Empty;
        var translation = new Services.ApiSurface.Repl.ReplSurfaceTranslator().Translate(friendly);
        ReplTechnicalPreview.Text = translation.TechnicalSource;

        var response = await _client.SendAsync(
            ProtocolJson.Create(MessageTypes.ExecuteCode, new ExecuteCodeRequest { code = translation.TechnicalSource }),
            TimeSpan.FromSeconds(30));

        if (response == null || response.type != MessageTypes.ExecuteResult)
        {
            ReplResultText.Text = "No result.";
            return;
        }

        var result = ProtocolJson.Payload<ExecuteCodeResponse>(response);
        ReplResultText.Text = result.success ? result.result ?? "(ok)" : result.error ?? "error";
        AppendLog(result.success ? $"REPL OK: {result.result}" : $"REPL error: {result.error}");
    }

    private async Task RefreshHudAsync()
    {
        if (!_client.IsConnected)
        {
            AppendLog("Not connected.");
            return;
        }

        var response = await _client.SendAsync(ProtocolJson.Create(MessageTypes.HudGetTree), TimeSpan.FromSeconds(10));
        if (response?.type != MessageTypes.HudTree)
        {
            AppendLog("HUD refresh failed.");
            return;
        }

        var tree = ProtocolJson.Payload<HudTreePayload>(response);
        Hierarchy.HudTree.Items.Clear();
        if (!tree.found)
        {
            AppendLog("FlightHud not found.");
            return;
        }

        foreach (var element in tree.elements)
        {
            Hierarchy.HudTree.Items.Add(BuildHudNode(element));
        }

        AppendLog("FlightHud tree loaded.");
    }

    private TreeViewItem BuildHudNode(HudElementNode node)
    {
        var item = new TreeViewItem
        {
            Header = string.IsNullOrWhiteSpace(node.text) ? node.path : $"{node.path} — \"{node.text}\"",
            Tag = node
        };

        foreach (var child in node.children)
        {
            item.Items.Add(BuildHudNode(child));
        }

        return item;
    }

    private void OnHudSelected()
    {
        if (Hierarchy.HudTree.SelectedItem is not TreeViewItem item || item.Tag is not HudElementNode node)
        {
            return;
        }

        _selectedHudInstanceId = node.instanceId;
        _selectedHudNode = node;
        _selectedSceneNode = null;
        _visualSelection = null;
        _inspectorMode = "hud";
        _suppressInspectorEvents = true;

        InspectorTargetText.Text = node.type;
        InspectorPathText.Text = node.path;
        InspectorPathText.IsVisible = true;
        InspectorTextBox.IsVisible = true;
        InspectorTextBox.Text = node.text;
        InspectorColorBox.IsVisible = true;
        InspectorColorBox.Text = "#FFFFFF";
        InspectorFontSizeBox.IsVisible = true;
        InspectorFontSizeBox.Text = "18";
        InspectorPositionPanel.IsVisible = false;
        InspectorLinePanel.IsVisible = false;
        InspectorRadiusBox.IsVisible = false;
        InspectorVisibleCheck.IsVisible = true;
        InspectorVisibleCheck.IsChecked = node.active;
        InspectorAutoApplyCheck.IsVisible = true;
        InspectorApplyButton.IsVisible = true;
        InspectorReadOnlyBox.IsVisible = false;
        _suppressInspectorEvents = false;
    }

    private async Task ApplyHudAsync()
    {
        if (!_client.IsConnected || !_selectedHudInstanceId.HasValue)
        {
            AppendLog("Select a HUD element first.");
            return;
        }

        float? fontSize = null;
        if (float.TryParse(InspectorFontSizeBox.Text, out var parsed))
        {
            fontSize = parsed;
        }

        var request = new HudUpdateRequest
        {
            instanceId = _selectedHudInstanceId.Value,
            active = InspectorVisibleCheck.IsChecked,
            colorHtml = string.IsNullOrWhiteSpace(InspectorColorBox.Text) ? null : InspectorColorBox.Text.Trim(),
            fontSize = fontSize,
            text = InspectorTextBox.Text
        };

        var response = await _client.SendAsync(ProtocolJson.Create(MessageTypes.HudUpdate, request), TimeSpan.FromSeconds(5));
        if (response?.type == MessageTypes.HudUpdated)
        {
            var updated = ProtocolJson.Payload<HudUpdateResponse>(response);
            AppendLog(updated.success ? "HUD updated." : $"HUD update failed: {updated.error}");
        }
    }

    private async Task SetOverlayAsync()
    {
        if (!_client.IsConnected)
        {
            return;
        }

        await _client.SendFireAndForgetAsync(ProtocolJson.Create(
            MessageTypes.OverlaySetEnabled,
            new OverlayEnabledRequest { enabled = OverlayEnabledCheck.IsChecked == true }));
    }

    private void GenerateHarmony()
    {
        var request = new HarmonyGenerateRequest
        {
            modNamespace = $"{ModBuilder.ModNameInput.Text?.Trim() ?? "MyMod"}_Engine",
            className = $"{ModBuilder.ModNameInput.Text?.Trim() ?? "MyMod"}Patch",
            targetType = ModBuilder.HarmonyTypeInput.Text?.Trim() ?? "FlightModel",
            methodName = ModBuilder.HarmonyMethodInput.Text?.Trim() ?? "Update",
            patchKind = (ModBuilder.HarmonyKindInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Prefix"
        };

        ModBuilder.HarmonySourceInput.Text = HarmonyPatchGenerator.Generate(request);
    }

    private async Task BuildModAsync()
    {
        var modName = ModBuilder.ModNameInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(modName))
        {
            AppendLog("Mod name required.");
            return;
        }

        var request = new ModBuildRequest
        {
            modName = modName,
            pluginGuid = string.IsNullOrWhiteSpace(ModBuilder.ModGuidInput.Text) ? $"com.at747.{modName.ToLowerInvariant()}" : ModBuilder.ModGuidInput.Text.Trim(),
            outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NuclearOptionSDK", "Mods", modName)
        };

        if (!string.IsNullOrWhiteSpace(ModBuilder.HarmonySourceInput.Text))
        {
            var patchPath = Path.Combine(request.outputDirectory, $"{modName}Patch.cs");
            Directory.CreateDirectory(request.outputDirectory);
            await File.WriteAllTextAsync(patchPath, ModBuilder.HarmonySourceInput.Text);
            request.extraSourceFiles = new[] { patchPath };
        }

        ModBuildResponse response;
        if (_client.IsConnected)
        {
            var envelope = await _client.SendAsync(ProtocolJson.Create(MessageTypes.ModBuild, request), TimeSpan.FromMinutes(2));
            response = envelope != null && envelope.type == MessageTypes.ModBuilt
                ? ProtocolJson.Payload<ModBuildResponse>(envelope)
                : new ModBuildResponse { success = false, error = "No mod.build response." };
        }
        else
        {
            response = ModProjectBuilder.Build(request, _settings.NuclearOptionRoot);
        }

        ModBuilder.ModBuildLogOutput.Text = response.buildLog;
        AppendLog(response.success
            ? $"Mod built: {response.outputPath}"
            : $"Mod build failed: {response.error}");
    }

    private async Task BuildVisualModAsync(bool showOverlay = false)
    {
        var modName = string.IsNullOrWhiteSpace(ModBuilder.ModNameInput.Text)
            ? "VisualHudMod"
            : ModBuilder.ModNameInput.Text.Trim();
        var guid = string.IsNullOrWhiteSpace(ModBuilder.ModGuidInput.Text)
            ? $"com.at747.{modName.ToLowerInvariant()}"
            : ModBuilder.ModGuidInput.Text.Trim();
        var output = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NuclearOptionSDK",
            "Mods",
            modName);

        var layout = VisualHudEditor.BuildLayoutPayload(modName);
        if (layout.labels.Length == 0 && layout.primitives.Length == 0)
        {
            AppendLog("Add at least one label or shape before building.");
            return;
        }

        var request = VisualHudModExporter.CreateBuildRequest(modName, guid, layout, output);
        var response = ModProjectBuilder.Build(request, _settings.NuclearOptionRoot);
        ModBuilder.ModBuildLogOutput.Text = response.buildLog;
        AppendLog(response.success
            ? $"Visual HUD mod built: {response.outputPath}"
            : $"Visual mod build failed: {response.error}");

        if (showOverlay)
        {
            ShowModBuildOverlay();
        }
    }

    private void HandlePush(MessageEnvelope envelope)
    {
        switch (envelope.type)
        {
            case MessageTypes.AudioEvent:
                var audio = ProtocolJson.Payload<AudioEventPayload>(envelope);
                AppendLog($"[audio] {audio.clipName} @ {audio.sourcePath}");
                break;
            case MessageTypes.Log:
                var log = ProtocolJson.Payload<LogPayload>(envelope);
                AppendLog($"[{log.level}] {log.message}");
                break;
            case MessageTypes.Error:
                var error = ProtocolJson.Payload<ErrorPayload>(envelope);
                AppendLog($"[error] {error.message}");
                break;
            case MessageTypes.BindingWatch:
                var watch = ProtocolJson.Payload<BindingWatchPayload>(envelope);
                ConstructorWorkspace.Logic.UpdateLiveTelemetry(watch);
                ProtocolLive.UpdateTelemetry(watch);
                break;
            case MessageTypes.TraceEvents:
                var trace = ProtocolJson.Payload<TraceEventsPayload>(envelope);
                ProtocolLive.UpdateTrace(trace);
                AppendLog($"[trace] events={trace.events.Length} active={trace.tracingActive}");
                break;
            case MessageTypes.DependencyRadarResult:
                var radar = ProtocolJson.Payload<DependencyRadarPayload>(envelope);
                var text = string.Join(
                    Environment.NewLine,
                    radar.warnings.Select(w => $"warning: {w}")
                        .Concat(radar.writers.Select(w => $"writer: {w.typeName}.{w.methodName}"))
                        .Concat(radar.readers.Select(r => $"reader: {r.typeName}.{r.methodName}")));
                ShowBottomPreview($"Dependency Radar: {radar.bindingId}", text);
                break;
            case MessageTypes.LogicStatus:
                var logicStatus = ProtocolJson.Payload<LogicStatusPayload>(envelope);
                if (logicStatus.lastActions.Length > 0)
                {
                    AppendLog($"[logic] fired {logicStatus.lastActions.Length} action(s)");
                }
                break;
        }
    }

    private async Task PushVisualHudPreviewAsync()
    {
        if (!_client.IsConnected)
        {
            AppendLog("Connect to Bridge before preview.");
            return;
        }

        OverlayEnabledCheck.IsChecked = true;
        await _client.SendFireAndForgetAsync(ProtocolJson.Create(
            MessageTypes.OverlaySetEnabled,
            new OverlayEnabledRequest { enabled = true }));

        var layout = VisualHudEditor.BuildLayoutPayload();
        await _client.SendFireAndForgetAsync(ProtocolJson.Create(MessageTypes.OverlayLayout, layout));
        StudioFileLogger.Info("visual-hud", $"preview labels={layout.labels.Length} shapes={layout.primitives.Length}");
        AppendLog($"Preview sent ({layout.labels.Length} labels, {layout.primitives.Length} shapes).");
    }

    private void AppendLog(string message)
    {
        StudioFileLogger.Info("ui", message);
        LogBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
    }

    private async Task StartTraceAsync()
    {
        if (!_client.IsConnected)
        {
            AppendLog("Not connected.");
            return;
        }

        var response = await _client.SendAsync(
            ProtocolJson.Create(MessageTypes.TraceStart, new TraceStartRequest { windowMs = 0 }),
            TimeSpan.FromSeconds(5));
        if (response?.type == MessageTypes.TraceEvents)
        {
            ProtocolLive.UpdateTrace(ProtocolJson.Payload<TraceEventsPayload>(response));
        }
        else
        {
            AppendLog(response == null
                ? "[trace] Start failed: no response (Bridge running?)"
                : $"[trace] Start failed: {response.type}");
        }
    }

    private async Task StopTraceAsync()
    {
        if (!_client.IsConnected)
        {
            AppendLog("Not connected.");
            return;
        }

        var response = await _client.SendAsync(ProtocolJson.Create(MessageTypes.TraceStop), TimeSpan.FromSeconds(5));
        if (response?.type == MessageTypes.TraceEvents)
        {
            ProtocolLive.UpdateTrace(ProtocolJson.Payload<TraceEventsPayload>(response));
        }
        else
        {
            AppendLog(response == null
                ? "[trace] Stop failed: no response"
                : $"[trace] Stop failed: {response.type}");
        }
    }

    private static string BuildLogPathLabel()
    {
        var text = $"Log: {StudioFileLogger.LogPath}";
#if STUDIO_UI_TRACE
        if (StudioUiInteractionTrace.IsEnabled && StudioUiInteractionTrace.LogPath != null)
        {
            text += $" | UI trace: {StudioUiInteractionTrace.LogPath}";
        }
#endif
        return text;
    }
}
