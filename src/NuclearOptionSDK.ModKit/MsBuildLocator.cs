using System.Diagnostics;
using System.Text;

namespace NuclearOptionSDK.ModKit;

/// <summary>Resolves MSBuild.exe across VS 2019–2026+ (including folder "18").</summary>
public static class MsBuildLocator
{
    public static string? TryResolve(StringBuilder? log = null)
    {
        var vsWhere = TryVsWhere();
        if (!string.IsNullOrWhiteSpace(vsWhere))
        {
            log?.AppendLine($"MSBuild: {vsWhere}");
            return vsWhere;
        }

        foreach (var candidate in EnumerateInstallPaths().OrderByDescending(ScoreInstallRoot))
        {
            if (File.Exists(candidate))
            {
                log?.AppendLine($"MSBuild: {candidate}");
                return candidate;
            }
        }

        return null;
    }

    private static string? TryVsWhere()
    {
        var vsWhere = ResolveVsWhereExe();
        if (vsWhere == null)
        {
            return null;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = vsWhere,
                Arguments = "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var first = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(File.Exists);
            return first;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveVsWhereExe()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Visual Studio", "Installer", "vswhere.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft Visual Studio", "Installer", "vswhere.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> EnumerateInstallPaths()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio")
        };

        var editions = new[] { "Enterprise", "Professional", "Community", "BuildTools", "Preview" };
        var relative = Path.Combine("MSBuild", "Current", "Bin", "MSBuild.exe");

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var versionDir in Directory.EnumerateDirectories(root))
            {
                foreach (var edition in editions)
                {
                    yield return Path.Combine(versionDir, edition, relative);
                }

                foreach (var editionDir in Directory.EnumerateDirectories(versionDir))
                {
                    yield return Path.Combine(editionDir, relative);
                }
            }
        }
    }

    private static int ScoreInstallRoot(string msbuildPath)
    {
        var parts = msbuildPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < parts.Length; i++)
        {
            if (!string.Equals(parts[i], "Microsoft Visual Studio", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= parts.Length)
            {
                break;
            }

            var versionToken = parts[i + 1];
            if (int.TryParse(versionToken, out var versionNumber))
            {
                // VS 2022 → folder "2022"; VS 2025/2026 → "17"/"18" (sort newer than 2022).
                return versionNumber >= 2000 ? versionNumber : 2100 + versionNumber;
            }

            break;
        }

        return 0;
    }
}
