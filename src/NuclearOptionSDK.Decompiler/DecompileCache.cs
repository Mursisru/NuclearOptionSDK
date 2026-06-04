using System.Security.Cryptography;
using System.Text;

namespace NuclearOptionSDK.Decompiler;

public sealed class DecompileCache
{
    private readonly string _root;

    public DecompileCache(string? rootDirectory = null)
    {
        _root = rootDirectory
                 ?? Path.Combine(
                     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "NuclearOptionSDK",
                     "decompile-cache");
        Directory.CreateDirectory(_root);
    }

    public bool TryRead(string assemblyPath, string typeFullName, string methodName, out string? content)
    {
        content = null;
        var path = GetCacheFilePath(assemblyPath, typeFullName, methodName);
        if (!File.Exists(path))
        {
            return false;
        }

        content = File.ReadAllText(path, Encoding.UTF8);
        return true;
    }

    public void Write(string assemblyPath, string typeFullName, string methodName, string content)
    {
        var path = GetCacheFilePath(assemblyPath, typeFullName, methodName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    public void InvalidateAssembly(string assemblyPath)
    {
        var hash = HashFile(assemblyPath);
        var dir = Path.Combine(_root, hash);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private string GetCacheFilePath(string assemblyPath, string typeFullName, string methodName)
    {
        var hash = HashFile(assemblyPath);
        var safeType = Sanitize(typeFullName);
        var safeMethod = Sanitize(methodName);
        return Path.Combine(_root, hash, safeType, $"{safeMethod}.cs");
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return sb.ToString();
    }

    private static string HashFile(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash)[..16];
    }
}
