using Avalonia.Controls;
using Avalonia.Interactivity;
using NuclearOptionSDK.Decompiler;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

public partial class SettingsWindow : Window
{
    public AppSettings Result { get; private set; } = new();

    public SettingsWindow()
    {
        InitializeComponent();
        SaveButton.Click += OnSave;
        CancelButton.Click += (_, _) => Close(false);
    }

    public void Load(AppSettings settings)
    {
        Result = settings;
        GamePathBox.Text = settings.NuclearOptionRoot;
        ReplDisclaimerCheck.IsChecked = settings.ReplDisclaimerAccepted;
        ErrorText.IsVisible = false;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        Result.NuclearOptionRoot = GamePathBox.Text?.Trim() ?? Result.NuclearOptionRoot;
        Result.ReplDisclaimerAccepted = ReplDisclaimerCheck.IsChecked == true;

        var validation = GameInstallValidator.Validate(Result.NuclearOptionRoot);
        if (!validation.IsValid)
        {
            ErrorText.Text = validation.Message;
            ErrorText.IsVisible = true;
            return;
        }

        Close(true);
    }
}
