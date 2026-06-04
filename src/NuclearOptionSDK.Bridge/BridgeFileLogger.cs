using System;
using System.IO;
using BepInEx;

namespace NuclearOptionSDK.Bridge;

public static class BridgeFileLogger
{
    private static readonly object Sync = new();
    private static string? _logPath;

    public static string LogPath => _logPath ?? Initialize(Paths.GameRootPath);

    public static string Initialize(string gameRoot)
    {
        var dir = Path.Combine(gameRoot, "BepInEx", "plugins", "NuclearOptionSDK_Data", "logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, $"bridge-{DateTime.Now:yyyy-MM-dd}.log");
        Info("system", "Bridge file logger initialized.");
        return _logPath;
    }

    public static void Info(string action, string message) => Write("INFO", action, message);
    public static void Warn(string action, string message) => Write("WARN", action, message);
    public static void Error(string action, string message) => Write("ERROR", action, message);

    private static void Write(string level, string action, string message)
    {
        if (_logPath == null)
        {
            return;
        }

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{action}] {message}";
        lock (Sync)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }
}
