using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using NuclearOptionSDK.Protocol;
using UnityEngine;

namespace NuclearOptionSDK.Bridge;

public sealed class RoslynScriptHost
{
    private readonly object _initLock = new();
    private ScriptOptions? _options;
    private ManualLogSource? _log;
    private string? _gameRoot;
    private string? _initError;

    public void Configure(ManualLogSource log, string gameRoot)
    {
        _log = log;
        _gameRoot = gameRoot;
    }

    public async Task<ExecuteCodeResponse> ExecuteAsync(string code)
    {
        if (!TryEnsureInitialized(out var initError))
        {
            return new ExecuteCodeResponse
            {
                success = false,
                error = initError
            };
        }

        try
        {
            var result = await CSharpScript.EvaluateAsync(code, _options!).ConfigureAwait(true);
            return new ExecuteCodeResponse
            {
                success = true,
                result = result?.ToString() ?? "(null)"
            };
        }
        catch (CompilationErrorException ex)
        {
            return new ExecuteCodeResponse
            {
                success = false,
                error = string.Join(Environment.NewLine, ex.Diagnostics)
            };
        }
        catch (Exception ex)
        {
            _log?.LogError(ex);
            return new ExecuteCodeResponse
            {
                success = false,
                error = ex.ToString()
            };
        }
    }

    private bool TryEnsureInitialized(out string? error)
    {
        if (_options != null)
        {
            error = null;
            return true;
        }

        lock (_initLock)
        {
            if (_options != null)
            {
                error = null;
                return true;
            }

            if (_initError != null)
            {
                error = _initError;
                return false;
            }

            try
            {
                _options = BuildOptions();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                _initError = $"Roslyn init failed: {ex.Message}";
                _log?.LogError(_initError);
                error = _initError;
                return false;
            }
        }
    }

    private ScriptOptions BuildOptions()
    {
        var refs = new List<MetadataReference>();
        AddAssemblyReference(refs, typeof(object).Assembly);
        AddAssemblyReference(refs, typeof(Enumerable).Assembly);
        AddAssemblyReference(refs, typeof(Debug).Assembly);
        AddAssemblyReference(refs, typeof(GameObject).Assembly);
        AddAssemblyReference(refs, typeof(BridgePlugin).Assembly);

        if (!string.IsNullOrWhiteSpace(_gameRoot))
        {
            var managed = Path.Combine(_gameRoot, "NuclearOption_Data", "Managed");
            AddFileReference(refs, Path.Combine(managed, "Assembly-CSharp.dll"));
            AddFileReference(refs, Path.Combine(managed, "UnityEngine.dll"));
            AddFileReference(refs, Path.Combine(managed, "UnityEngine.CoreModule.dll"));
            AddFileReference(refs, Path.Combine(managed, "UnityEngine.UIModule.dll"));
            AddFileReference(refs, Path.Combine(managed, "Unity.TextMeshPro.dll"));
        }

        return ScriptOptions.Default
            .AddReferences(refs)
            .AddImports(
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "UnityEngine");
    }

    private static void AddAssemblyReference(List<MetadataReference> refs, Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly.Location))
        {
            return;
        }

        refs.Add(MetadataReference.CreateFromFile(assembly.Location));
    }

    private static void AddFileReference(List<MetadataReference> refs, string path)
    {
        if (File.Exists(path))
        {
            refs.Add(MetadataReference.CreateFromFile(path));
        }
    }
}
