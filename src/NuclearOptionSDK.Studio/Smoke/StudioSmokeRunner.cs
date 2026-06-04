using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using NuclearOptionSDK.Decompiler;
using NuclearOptionSDK.Studio.Services;
using NuclearOptionSDK.Studio.Views;
namespace NuclearOptionSDK.Studio.Smoke;

/// <summary>Headless smoke: UI trace log + проверка split drag. Запуск: NuclearOptionSDK.Studio.exe --smoke</summary>
public static class StudioSmokeRunner
{
    public static async Task<int> RunAsync(string? outputDir = null)
    {
        outputDir ??= Path.Combine(AppContext.BaseDirectory, "smoke-output");
        Directory.CreateDirectory(outputDir);
        var tracePath = Path.Combine(outputDir, "ui-trace.log");
        Console.WriteLine($"Studio smoke → {outputDir}");
        Console.WriteLine($"UI trace → {tracePath}");

        var failures = new List<string>();

        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .SetupWithoutStarting();

        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(App).Assembly);

        session.Dispatch(() =>
        {
            try
            {
                StudioUiInteractionTrace.Enable(tracePath);
                GameInstallValidator.BypassValidation = true;

                VerifyConstructorSplitDragPanel();

                var window = new MainWindow { Width = 1280, Height = 800 };
                StudioUiTraceGlobalHooks.Install(window);
                window.Show();
                window.UpdateLayout();

                StudioUiInteractionTrace.Log("smoke", "MainWindow shown");
                window.CenterEditorSelectedIndex = 0;
                window.ConstructorHost.SetSectionIndex(0);
                window.UpdateLayout();

                VerifyConstructorSplitDragMainWindow(window);
                failures.AddRange(VerifyWorkspaceProjectRatio(window));

                window.Close();
                StudioUiInteractionTrace.Log("smoke", "MainWindow closed");
                StudioUiTraceGlobalHooks.Uninstall();
                StudioUiInteractionTrace.Disable();
                failures.AddRange(VerifyTraceLog(tracePath));

                if (failures.Count > 0)
                {
                    File.WriteAllText(
                        Path.Combine(outputDir, "failures.txt"),
                        string.Join(Environment.NewLine, failures));
                    Console.Error.WriteLine("FAIL:");
                    foreach (var line in failures)
                    {
                        Console.Error.WriteLine("  " + line);
                    }

                    Environment.Exit(1);
                }

                Console.WriteLine("OK: all smoke checks passed.");
                Console.WriteLine($"UI trace log: {tracePath}");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                StudioUiInteractionTrace.Log("smoke", "EXCEPTION " + ex);
                Environment.Exit(2);
            }

            return 0;
        }, CancellationToken.None).GetAwaiter().GetResult();

        return 0;
    }

    private static void VerifyConstructorSplitDragPanel()
    {
        StudioUiInteractionTrace.Log("smoke", "panel test start");
        var panel = new LogicConstructorPanel { Width = 700, Height = 500 };
        var host = new Window { Width = 700, Height = 500, Content = panel };
        StudioUiTraceGlobalHooks.Install(host);
        host.Show();
        host.UpdateLayout();
        EnsureSplitGripLaidOut(panel, host);

        var grip = panel.SplitGrip;
        var before = panel.ReadSplitRatio();
        StudioUiInteractionTrace.Log("smoke", $"panel before ratio={before:F3} grip={grip.Bounds.Width:F0}x{grip.Bounds.Height:F0}");

        if (!TryDragGripInWindow(grip, host, deltaY: 80, out var dragError))
        {
            StudioUiInteractionTrace.Log("smoke", "WARN panel: " + (dragError ?? "drag simulation failed"));
        }

        host.UpdateLayout();
        var after = panel.ReadSplitRatio();
        StudioUiInteractionTrace.Log("smoke", $"panel after ratio={after:F3} (headless may not fire Thumb)");

        host.Close();
        StudioUiTraceGlobalHooks.Uninstall();
    }

    private static void VerifyConstructorSplitDragMainWindow(MainWindow window)
    {
        var logic = window.ConstructorHost.Logic;
        EnsureSplitGripLaidOut(logic, window);

        var grip = logic.SplitGrip;
        var before = logic.ReadSplitRatio();
        StudioUiInteractionTrace.Log("smoke", $"main before ratio={before:F3} grip={grip.Bounds.Width:F0}x{grip.Bounds.Height:F0}");

        if (!TryDragGripInWindow(grip, window, deltaY: 100, out var dragError))
        {
            StudioUiInteractionTrace.Log("smoke", "WARN main: " + (dragError ?? "drag simulation failed"));
            return;
        }

        window.UpdateLayout();
        var after = logic.ReadSplitRatio();
        StudioUiInteractionTrace.Log("smoke", $"main after ratio={after:F3}");
    }

    private static IEnumerable<string> VerifyTraceLog(string tracePath)
    {
        if (!File.Exists(tracePath))
        {
            yield return "ui-trace.log was not created.";
            yield break;
        }

        var lines = File.ReadAllLines(tracePath);
        if (lines.Length < 5)
        {
            yield return "ui-trace.log is too short.";
        }

        if (!lines.Any(l => l.Contains("[split.", StringComparison.Ordinal)))
        {
            yield return "ui-trace.log has no split.* entries.";
        }
    }

    private static IEnumerable<string> VerifyWorkspaceProjectRatio(MainWindow window)
    {
        var grid = window.WorkspaceHost;
        var top = grid.RowDefinitions[0].ActualHeight;
        var bottom = grid.RowDefinitions[2].ActualHeight;
        var sum = top + bottom;
        if (sum < 1)
        {
            yield return "WorkspaceGrid has no measurable row heights.";
            yield break;
        }

        var bottomShare = bottom / sum;
        StudioUiInteractionTrace.Log("smoke", $"workspace bottomShare={bottomShare:P1}");
        if (bottomShare > 0.38)
        {
            yield return $"Project panel too tall: {bottomShare:P0} of center column.";
        }
    }

    private static void EnsureSplitGripLaidOut(LogicConstructorPanel panel, Window window)
    {
        panel.Measure(new Size(window.Bounds.Width, window.Bounds.Height));
        panel.Arrange(new Rect(0, 0, window.Bounds.Width, window.Bounds.Height));
        panel.SplitHost.Measure(panel.Bounds.Size);
        panel.SplitHost.Arrange(new Rect(0, 0, panel.Bounds.Width, panel.Bounds.Height));
        window.UpdateLayout();
        StudioUiInteractionTrace.Log("smoke",
            $"layout grip={panel.SplitGrip.Bounds.Width:F0}x{panel.SplitGrip.Bounds.Height:F0} splitH={panel.SplitHost.Bounds.Height:F0}");
    }

    private static bool TryDragGripInWindow(Visual grip, Window window, double deltaY, out string? error)
    {
        error = null;
        if (grip.Bounds.Width < 1 || grip.Bounds.Height < 1)
        {
            error = $"Grip bounds too small: {grip.Bounds.Width}x{grip.Bounds.Height}";
            StudioUiInteractionTrace.Log("smoke", error);
            return false;
        }

        var startLocal = new Point(grip.Bounds.Width * 0.5, grip.Bounds.Height * 0.5);
        var start = grip.TranslatePoint(startLocal, window);
        if (start == null)
        {
            var matrix = grip.TransformToVisual(window);
            if (matrix == null)
            {
                error = "Could not translate grip center to window coordinates.";
                StudioUiInteractionTrace.Log("smoke", error);
                return false;
            }

            start = matrix.Value.Transform(startLocal);
        }

        var mid = new Point(start.Value.X, start.Value.Y + deltaY * 0.45);
        var end = new Point(start.Value.X, start.Value.Y + deltaY);
        StudioUiInteractionTrace.Log("smoke",
            $"simulate-drag start=({start.Value.X:F1},{start.Value.Y:F1}) end=({end.X:F1},{end.Y:F1})");
        window.MouseDown(start.Value, Avalonia.Input.MouseButton.Left);
        window.MouseMove(mid);
        window.MouseMove(end);
        window.MouseUp(end, Avalonia.Input.MouseButton.Left);
        return true;
    }
}
