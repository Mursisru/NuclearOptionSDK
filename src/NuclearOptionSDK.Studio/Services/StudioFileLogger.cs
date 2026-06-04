namespace NuclearOptionSDK.Studio.Services;

public static class StudioFileLogger
{
    private static readonly object Sync = new();
    private static string? _logPath;

    public static string LogPath
    {
        get
        {
            if (_logPath != null)
            {
                return _logPath;
            }

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NuclearOptionSDK",
                "logs");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, $"studio-{DateTime.Now:yyyy-MM-dd}.log");
            Info("system", "Studio file logger initialized.");
            return _logPath;
        }
    }

    public static void Info(string action, string message) => Write("INFO", action, message);
    public static void Warn(string action, string message) => Write("WARN", action, message);
    public static void Error(string action, string message) => Write("ERROR", action, message);

    private static void Write(string level, string action, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{action}] {message}";
        lock (Sync)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
    }
}
