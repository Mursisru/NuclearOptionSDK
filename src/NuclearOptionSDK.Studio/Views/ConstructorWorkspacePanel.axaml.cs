namespace NuclearOptionSDK.Studio.Views;



using Avalonia.Controls;



public partial class ConstructorWorkspacePanel : UserControl

{

    private int _selectedSectionIndex;



    public ConstructorWorkspacePanel()

    {

        InitializeComponent();

        SectionTabs.SelectionChanged += (_, _) => SetSectionIndex(SectionTabs.SelectedIndex);

        SetSectionIndex(0);

    }



    public event EventHandler? SelectedSectionChanged;



    public int SelectedSectionIndex => _selectedSectionIndex;



    public LogicConstructorPanel Logic => LogicConstructor;

    public AudioConstructorPanel Audio => AudioConstructor;

    public GraphConstructorPanel Mechanic => MechanicConstructor;

    public GraphConstructorPanel Qol => QolConstructor;



    public void SelectSection(string section) =>

        SetSectionIndex(section switch

        {

            "audio" => 1,

            "mechanic" => 2,

            "qol" => 3,

            _ => 0

        });



    public void SetSectionIndex(int index)

    {

        index = Math.Clamp(index, 0, 3);

        if (_selectedSectionIndex == index)

        {

            return;

        }



        _selectedSectionIndex = index;

        LogicConstructor.IsVisible = index == 0;

        AudioConstructor.IsVisible = index == 1;

        MechanicConstructor.IsVisible = index == 2;

        QolConstructor.IsVisible = index == 3;



        if (SectionTabs.SelectedIndex != index)

        {

            SectionTabs.SelectedIndex = index;

        }



        SelectedSectionChanged?.Invoke(this, EventArgs.Empty);

    }

}

