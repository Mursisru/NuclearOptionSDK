using System.Globalization;
using System.Text;
using Avalonia.Controls;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>
/// Подробный лог каждого UI-действия (мышь, drag split, layout). Только при STUDIO_UI_TRACE.
/// </summary>
public static class StudioUiInteractionTrace
{
    private static readonly object Sync = new();

    public static bool IsEnabled { get; private set; }

#if STUDIO_UI_TRACE
    private static StreamWriter? _writer;
    private static string? _logPath;
#endif

    public static string? LogPath =>
#if STUDIO_UI_TRACE
        _logPath;
#else
        null;
#endif

    public static void Enable(string? logFilePath = null)
    {
#if STUDIO_UI_TRACE
        lock (Sync)
        {
            if (IsEnabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "NuclearOptionSDK",
                    "logs");
                Directory.CreateDirectory(dir);
                logFilePath = Path.Combine(dir, $"ui-trace-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            }
            else
            {
                var dir = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            _logPath = logFilePath;
            var stream = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            IsEnabled = true;
            WriteLine("session", $"START pid={Environment.ProcessId} trace={_logPath}");
            WriteLine("session", $"cmdline={Environment.CommandLine}");
        }
#else
        _ = logFilePath;
#endif
    }

    public static void Disable()
    {
#if STUDIO_UI_TRACE
        lock (Sync)
        {
            if (!IsEnabled)
            {
                return;
            }

            WriteLine("session", "END");
            _writer?.Dispose();
            _writer = null;
            IsEnabled = false;
        }
#endif
    }

    public static void Log(string category, string message)
    {
#if STUDIO_UI_TRACE
        if (!IsEnabled)
        {
            return;
        }

        WriteLine(category, message);
#endif
    }

    public static void LogGridRows(string category, Grid grid, string? note = null)
    {
#if STUDIO_UI_TRACE
        if (!IsEnabled || grid.RowDefinitions.Count == 0)
        {
            return;
        }

        var parts = new StringBuilder();
        parts.Append($"gridH={grid.Bounds.Height:F0}");
        if (!string.IsNullOrEmpty(note))
        {
            parts.Append(' ').Append(note);
        }

        for (var i = 0; i < grid.RowDefinitions.Count; i++)
        {
            var row = grid.RowDefinitions[i];
            parts.Append(CultureInfo.InvariantCulture,
                $" | r{i}:{row.Height.Value:F1}{UnitSuffix(row.Height.GridUnitType)} act={row.ActualHeight:F1}");
        }

        Log(category, parts.ToString());
#endif
    }

#if STUDIO_UI_TRACE
    private static string UnitSuffix(GridUnitType unit) =>
        unit switch
        {
            GridUnitType.Star => "*",
            GridUnitType.Pixel => "px",
            GridUnitType.Auto => "auto",
            _ => "?"
        };

    private static void WriteLine(string category, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [UI] [{category}] {message}";
        lock (Sync)
        {
            _writer?.WriteLine(line);
        }

        Console.WriteLine(line);
    }
#endif
}
