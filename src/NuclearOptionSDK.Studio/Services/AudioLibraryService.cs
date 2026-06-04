using System.Media;
using System.Runtime.Versioning;

namespace NuclearOptionSDK.Studio.Services;

public sealed class AudioClipEntry
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public long SizeBytes { get; init; }
}

[SupportedOSPlatform("windows")]
public static class AudioLibraryService
{
    private static readonly object PlayLock = new();
    private static SoundPlayer? _player;

    public static string LibraryDir =>
        Path.Combine(LogicProjectStore.NosdkDir, "audio");

    public static string BackupDir =>
        Path.Combine(LibraryDir, "_backup");

    public static void EnsureDirs()
    {
        Directory.CreateDirectory(LibraryDir);
        Directory.CreateDirectory(BackupDir);
    }

    public static IReadOnlyList<AudioClipEntry> ListClips()
    {
        EnsureDirs();
        return Directory.EnumerateFiles(LibraryDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => IsAudioExt(f))
            .Select(f => new FileInfo(f))
            .Select(fi => new AudioClipEntry
            {
                FileName = fi.Name,
                FullPath = fi.FullName,
                SizeBytes = fi.Length
            })
            .OrderBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ImportFile(string sourcePath)
    {
        EnsureDirs();
        var name = Path.GetFileName(sourcePath);
        var dest = Path.Combine(LibraryDir, name);
        if (File.Exists(dest))
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            dest = Path.Combine(LibraryDir, $"{stem}_{DateTime.Now:HHmmss}{ext}");
        }

        File.Copy(sourcePath, dest, overwrite: false);
        return dest;
    }

    public static void DeleteClip(string fileName)
    {
        EnsureDirs();
        var path = Path.Combine(LibraryDir, fileName);
        if (!File.Exists(path))
        {
            return;
        }

        var backupPath = Path.Combine(BackupDir, fileName);
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        File.Move(path, backupPath);
    }

    public static int RestoreAll()
    {
        EnsureDirs();
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(BackupDir, "*.*", SearchOption.TopDirectoryOnly))
        {
            if (!IsAudioExt(file))
            {
                continue;
            }

            var dest = Path.Combine(LibraryDir, Path.GetFileName(file));
            if (File.Exists(dest))
            {
                continue;
            }

            File.Move(file, dest);
            count++;
        }

        return count;
    }

    public static void Play(string fileName)
    {
        var path = Path.Combine(LibraryDir, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Audio clip not found.", path);
        }

        lock (PlayLock)
        {
            _player?.Dispose();
            _player = new SoundPlayer(path);
            _player.Load();
            _player.Play();
        }
    }

    public static void Stop()
    {
        lock (PlayLock)
        {
            _player?.Stop();
        }
    }

    private static bool IsAudioExt(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".wav" or ".wave";
    }
}
