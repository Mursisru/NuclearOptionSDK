using Avalonia.Controls;

namespace NuclearOptionSDK.Studio.Views;

public partial class ModBuilderPanel : UserControl
{
    public ModBuilderPanel()
    {
        InitializeComponent();
    }

    public TextBox ModNameInput => ModNameBox;
    public TextBox ModGuidInput => ModGuidBox;
    public TextBox HarmonyTypeInput => HarmonyTypeBox;
    public TextBox HarmonyMethodInput => HarmonyMethodBox;
    public ComboBox HarmonyKindInput => HarmonyKindBox;
    public TextBox HarmonySourceInput => HarmonySourceBox;
    public TextBox ModBuildLogOutput => ModBuildLogBox;
    public Button GenerateHarmonyBtn => GenerateHarmonyButton;
    public Button BuildModBtn => BuildModButton;
}
