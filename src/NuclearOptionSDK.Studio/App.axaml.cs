using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NuclearOptionSDK.Decompiler;
using NuclearOptionSDK.Studio.Services;
using NuclearOptionSDK.Studio.Views;

namespace NuclearOptionSDK.Studio;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (IsSmokeRun())
            {
                GameInstallValidator.BypassValidation = true;
                desktop.MainWindow = new MainWindow();
#if STUDIO_UI_TRACE
                StudioUiInteractionTrace.Enable();
                StudioUiTraceGlobalHooks.Install(desktop.MainWindow);
#endif
            }
            else
            {
                var settings = AppSettingsStore.Load();
                var validation = GameInstallValidator.Validate(settings.NuclearOptionRoot);
                desktop.MainWindow = validation.IsValid
                    ? new MainWindow()
                    : new GameRequiredWindow();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool IsSmokeRun() =>
        Environment.GetCommandLineArgs().Any(a =>
            a.Equals("--smoke", StringComparison.OrdinalIgnoreCase));
}