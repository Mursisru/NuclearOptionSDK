using Avalonia.Controls;

namespace NuclearOptionSDK.Studio.Views;

public partial class HierarchyPanel : UserControl
{
    public HierarchyPanel()
    {
        InitializeComponent();
    }

    public TreeView SceneTree => SceneTreeControl;
    public TreeView HudTree => HudTreeControl;
    public Button RefreshSceneButton => RefreshSceneButtonControl;
    public Button RefreshHudButton => RefreshHudButtonControl;
    public TextBox SceneSearchBox => SceneSearchBoxControl;
    public GameCodePanel GameCode => GameCodeEditor;
    public LogicPalettePanel LogicPalette => LogicPaletteEditor;
    public TabControl ContentTabs => HierarchyTabs;

    public void SelectTab(HierarchyTab tab) => HierarchyTabs.SelectedIndex = (int)tab;
}

public enum HierarchyTab
{
    Scene = 0,
    Hud = 1,
    Code = 2,
    Blocks = 3
}
