using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilModuleDefinition = Mono.Cecil.ModuleDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;

namespace NuclearOptionSDK.Decompiler;

public sealed class IlSpyDecompileService : IDecompileService
{
    private readonly DecompileCache _cache = new();
    private readonly object _gate = new();

    public Task<string?> DecompileMethodAsync(
        string? nuclearOptionRoot,
        string typeFullName,
        string methodName,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assemblyPath = GameAssemblyPaths.ResolveMainAssembly(nuclearOptionRoot);
            if (assemblyPath == null)
            {
                return null;
            }

            if (!bypassCache && _cache.TryRead(assemblyPath, typeFullName, methodName, out var cached))
            {
                return cached;
            }

            var managedDir = GameAssemblyPaths.ResolveManagedDirectory(nuclearOptionRoot);
            if (managedDir == null)
            {
                return null;
            }

            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var module = CecilModuleDefinition.ReadModule(assemblyPath);
                var type = TypeResolver.Resolve(module, typeFullName);
                if (type == null)
                {
                    return null;
                }

                var method = MethodResolver.Resolve(type, methodName);
                if (method == null)
                {
                    return null;
                }

                var code = DecompileMethod(assemblyPath, managedDir, type.FullName.Replace('/', '.'), method);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    _cache.Write(assemblyPath, typeFullName, methodName, code);
                }

                return code;
            }
        }, cancellationToken);
    }

    public Task<string?> DecompileTypeAsync(
        string? nuclearOptionRoot,
        string typeFullName,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assemblyPath = GameAssemblyPaths.ResolveMainAssembly(nuclearOptionRoot);
            if (assemblyPath == null)
            {
                return null;
            }

            const string typeMarker = "__type__";
            if (!bypassCache && _cache.TryRead(assemblyPath, typeFullName, typeMarker, out var cached))
            {
                return cached;
            }

            var managedDir = GameAssemblyPaths.ResolveManagedDirectory(nuclearOptionRoot);
            if (managedDir == null)
            {
                return null;
            }

            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var module = CecilModuleDefinition.ReadModule(assemblyPath);
                var type = TypeResolver.Resolve(module, typeFullName);
                if (type == null)
                {
                    return null;
                }

                var resolvedName = type.FullName.Replace('/', '.');
                var code = DecompileType(assemblyPath, managedDir, resolvedName);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    _cache.Write(assemblyPath, typeFullName, typeMarker, code);
                }

                return code;
            }
        }, cancellationToken);
    }

    private static string? DecompileMethod(
        string assemblyPath,
        string managedDir,
        string typeFullName,
        CecilMethodDefinition method)
    {
        var decompiler = CreateDecompiler(assemblyPath, managedDir);
        try
        {
            var type = ResolveDecompilerType(decompiler, typeFullName);
            if (type == null)
            {
                return null;
            }

            var parameterCount = method.Parameters.Count;
            foreach (var candidate in type.Methods)
            {
                if (candidate.IsConstructor
                    || !string.Equals(candidate.Name, method.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (candidate.Parameters.Count == parameterCount)
                {
                    return decompiler.DecompileAsString(candidate.MetadataToken);
                }
            }

            var fallback = type.Methods.FirstOrDefault(m =>
                !m.IsConstructor && string.Equals(m.Name, method.Name, StringComparison.Ordinal));
            return fallback == null ? null : decompiler.DecompileAsString(fallback.MetadataToken);
        }
        catch
        {
            return null;
        }
    }

    private static string? DecompileType(string assemblyPath, string managedDir, string fullTypeName)
    {
        var decompiler = CreateDecompiler(assemblyPath, managedDir);
        try
        {
            return decompiler.DecompileTypeAsString(new FullTypeName(fullTypeName));
        }
        catch
        {
            return null;
        }
    }

    private static ITypeDefinition? ResolveDecompilerType(CSharpDecompiler decompiler, string typeFullName)
    {
        var type = decompiler.TypeSystem.FindType(new FullTypeName(typeFullName)).GetDefinition();
        if (type != null)
        {
            return type;
        }

        var shortName = typeFullName.Contains('.')
            ? typeFullName[(typeFullName.LastIndexOf('.') + 1)..]
            : typeFullName;

        return decompiler.TypeSystem.MainModule.TypeDefinitions
            .FirstOrDefault(t => string.Equals(t.Name, shortName, StringComparison.Ordinal));
    }

    private static CSharpDecompiler CreateDecompiler(string assemblyPath, string managedDir)
    {
        var settings = new DecompilerSettings(LanguageVersion.Latest)
        {
            ThrowOnAssemblyResolveErrors = false,
            UseSdkStyleProjectFormat = false
        };

        var resolver = new UniversalAssemblyResolver(assemblyPath, false, null);
        foreach (var reference in GameAssemblyPaths.EnumerateReferenceAssemblies(managedDir))
        {
            resolver.AddSearchDirectory(Path.GetDirectoryName(reference)!);
        }

        resolver.AddSearchDirectory(managedDir);
        return new CSharpDecompiler(assemblyPath, resolver, settings);
    }
}

internal static class TypeResolver
{
    public static CecilTypeDefinition? Resolve(CecilModuleDefinition module, string typeFullName)
    {
        if (string.IsNullOrWhiteSpace(typeFullName))
        {
            return null;
        }

        var normalized = typeFullName.Replace('+', '/');
        foreach (var type in module.Types)
        {
            var match = ResolveRecursive(type, normalized);
            if (match != null)
            {
                return match;
            }
        }

        var shortName = normalized.Contains('.')
            ? normalized[(normalized.LastIndexOf('.') + 1)..]
            : normalized;

        foreach (var type in module.Types)
        {
            var match = ResolveByShortName(type, shortName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static CecilTypeDefinition? ResolveRecursive(CecilTypeDefinition type, string fullName)
    {
        var name = type.FullName.Replace('/', '.');
        if (string.Equals(name, fullName, StringComparison.Ordinal)
            || string.Equals(type.Name, fullName, StringComparison.Ordinal))
        {
            return type;
        }

        if (name.EndsWith('.' + fullName, StringComparison.Ordinal))
        {
            return type;
        }

        foreach (var nested in type.NestedTypes)
        {
            var match = ResolveRecursive(nested, fullName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static CecilTypeDefinition? ResolveByShortName(CecilTypeDefinition type, string shortName)
    {
        if (string.Equals(type.Name, shortName, StringComparison.Ordinal))
        {
            return type;
        }

        foreach (var nested in type.NestedTypes)
        {
            var match = ResolveByShortName(nested, shortName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}

internal static class MethodResolver
{
    public static CecilMethodDefinition? Resolve(CecilTypeDefinition type, string methodName)
    {
        var candidates = type.Methods
            .Where(m => !m.IsConstructor
                        && string.Equals(m.Name, methodName, StringComparison.Ordinal)
                        && m.HasBody)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = type.Methods
                .Where(m => !m.IsConstructor
                            && string.Equals(m.Name, methodName, StringComparison.Ordinal))
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(m => m.Body?.Instructions.Count ?? 0)
            .ThenBy(m => m.IsStatic ? 1 : 0)
            .First();
    }
}
