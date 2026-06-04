using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NuclearOptionSDK.Decompiler;
using NuclearOptionSDK.Studio.Services;

namespace NuclearOptionSDK.Studio.Views;

public partial class GameRequiredWindow : Window
{
    private readonly IlSpyDecompileService _decompile = new();
    private GameInstallValidation? _lastValidation;

    public GameRequiredWindow()
    {
        InitializeComponent();
        Title = $"Nuclear Studio — {AppVersion.Display}";

        var settings = AppSettingsStore.Load();
        GamePathBox.Text = settings.NuclearOptionRoot;

        StatusText.Text =
            "Nuclear Option must be installed with Assembly-CSharp.dll.\n" +
            "File dumps are not used — Studio reads and decompiles from the game only.";

        BrowseButton.Click += OnBrowse;
        VerifyButton.Click += async (_, _) => await VerifyAsync();
        ContinueButton.Click += OnContinue;
        ExitButton.Click += (_, _) =>
        {
            if (Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        };

        Closing += (_, e) =>
        {
            if (!ContinueButton.IsEnabled)
            {
                e.Cancel = true;
            }
        };

        _ = VerifyAsync();
    }

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Nuclear Option folder",
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            GamePathBox.Text = path;
            await VerifyAsync();
        }
    }

    private async Task VerifyAsync()
    {
        DetailText.Text = "Verifying…";
        ContinueButton.IsEnabled = false;
        _lastValidation = null;

        var root = GamePathBox.Text?.Trim() ?? string.Empty;
        var install = GameInstallValidator.Validate(root);
        if (!install.IsValid)
        {
            DetailText.Text = install.Message;
            return;
        }

        DetailText.Text = install.Message + "\n" + install.AssemblyPath + "\n\nTesting decompiler…";

        var sample = await _decompile.DecompileMethodAsync(root, "Aircraft", "Update")
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(sample))
        {
            sample = await _decompile.DecompileMethodAsync(root, "Aircraft", "Awake").ConfigureAwait(true);
        }

        if (string.IsNullOrWhiteSpace(sample) || sample.Length < 80 || !sample.Contains('{'))
        {
            DetailText.Text =
                "DLL found but decompilation failed.\n" +
                "Check Managed\\ integrity and file permissions.";
            return;
        }

        _lastValidation = install;
        DetailText.Text =
            "Game and decompiler OK.\n" +
            $"Sample: Aircraft ({sample.Length} chars of C#).";
        ContinueButton.IsEnabled = true;
    }

    private void OnContinue(object? sender, RoutedEventArgs e)
    {
        if (_lastValidation is not { IsValid: true })
        {
            return;
        }

        var settings = AppSettingsStore.Load();
        settings.NuclearOptionRoot = GamePathBox.Text?.Trim() ?? settings.NuclearOptionRoot;
        AppSettingsStore.Save(settings);

        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.Show();
            Close();
        }
    }
}
