using Avalonia;
using Avalonia.Headless;
using NuclearOptionSDK.Studio;

[assembly: AvaloniaTestApplication(typeof(NuclearOptionSDK.Studio.Tests.StudioTestAppBuilder))]

namespace NuclearOptionSDK.Studio.Tests;

public static class StudioTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

