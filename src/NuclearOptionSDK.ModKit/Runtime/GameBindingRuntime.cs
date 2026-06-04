using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NuclearOptionSDK.GameBindings;

/// <summary>
/// Reflection read/write for Member.* bindings (fields, properties, and parameterless bool methods like IsLanded).
/// Copied into each built mod; namespace rewritten at build time.
/// </summary>
public static class GameBindingRuntime
{
    private static readonly Dictionary<string, MemberInfo?> MemberCache = new(StringComparer.Ordinal);

    private static Type? _gameManagerType;
    private static MethodInfo? _getLocalAircraftMethod;

    public static bool TryGetLocalAircraft(out object? aircraft)
    {
        aircraft = null;
        try
        {
            if (!TryResolveGetLocalAircraft(out var method))
            {
                return false;
            }

            var args = new object?[] { null };
            var ok = (bool)method.Invoke(null, args)!;
            aircraft = args[0];
            return ok && aircraft != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveGetLocalAircraft(out MethodInfo method)
    {
        method = _getLocalAircraftMethod!;
        if (method != null)
        {
            return true;
        }

        var gmType = ResolveGameType("GameManager");
        if (gmType == null)
        {
            return false;
        }

        var resolved = gmType.GetMethod("GetLocalAircraft", BindingFlags.Public | BindingFlags.Static);
        if (resolved == null)
        {
            return false;
        }

        _gameManagerType = gmType;
        _getLocalAircraftMethod = method = resolved;
        return true;
    }

    private static Type? ResolveGameType(string typeName)
    {
        if (_gameManagerType != null && _gameManagerType.Name == typeName)
        {
            return _gameManagerType;
        }

        var fromGetType = Type.GetType(typeName + ", Assembly-CSharp");
        if (fromGetType != null)
        {
            return fromGetType;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (name == null || (!name.StartsWith("Assembly-CSharp", StringComparison.Ordinal)
                                 && !name.Equals("Assembly-CSharp", StringComparison.Ordinal)))
            {
                continue;
            }

            var t = asm.GetType(typeName);
            if (t != null)
            {
                return t;
            }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(typeName);
            if (t != null)
            {
                return t;
            }
        }

        return null;
    }

    public static bool ReadBool(object root, string bindingId)
    {
        var v = ReadValue(root, bindingId);
        return v switch
        {
            bool b => b,
            float f => Math.Abs(f) > 1e-6f,
            double d => Math.Abs(d) > 1e-6,
            int i => i != 0,
            _ => false
        };
    }

    public static float ReadFloat(object root, string bindingId)
    {
        var v = ReadValue(root, bindingId);
        return v switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            bool b => b ? 1f : 0f,
            _ => 0f
        };
    }

    public static bool ApplyWrite(object root, string bindingId, object? value)
    {
        if (value == null || root == null)
        {
            return false;
        }

        if (bindingId.StartsWith("Method.", StringComparison.Ordinal))
        {
            return InvokeMethodBinding(root, bindingId, value);
        }

        var path = NormalizeMemberPath(bindingId);
        if (!TryResolveLeaf(root, path, out var parent, out var leafName) || parent == null)
        {
            return false;
        }

        if (TryInvokePreferredBoolSetter(parent, leafName, value))
        {
            return true;
        }

        if (!TryResolveMember(parent.GetType(), leafName, out var member))
        {
            return false;
        }

        try
        {
            switch (member)
            {
                case FieldInfo fi when !fi.IsInitOnly:
                    fi.SetValue(parent, Convert.ChangeType(value, fi.FieldType));
                    return true;
                case PropertyInfo pi when pi.CanWrite:
                    pi.SetValue(parent, Convert.ChangeType(value, pi.PropertyType));
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static object? ReadValue(object root, string bindingId)
    {
        if (root == null)
        {
            return null;
        }

        if (bindingId.StartsWith("Method.", StringComparison.Ordinal))
        {
            return InvokeMethodBindingValue(root, bindingId);
        }

        var path = NormalizeMemberPath(bindingId);
        if (!TryResolveLeaf(root, path, out var parent, out var leafName) || parent == null)
        {
            return null;
        }

        if (TryInvokeBoolMethod(parent, leafName, out var boolResult))
        {
            return boolResult;
        }

        if (!TryResolveMember(parent.GetType(), leafName, out var member))
        {
            return null;
        }

        return member switch
        {
            FieldInfo fi => fi.GetValue(parent),
            PropertyInfo pi => pi.GetValue(parent),
            _ => null
        };
    }

    private static bool InvokeMethodBinding(object root, string bindingId, object value)
    {
        // Method.Aircraft.SetGear -> SetGear(bool) on aircraft
        var path = bindingId.Substring("Method.".Length);
        if (!TryResolveLeaf(root, path, out var parent, out var methodName) || parent == null)
        {
            return false;
        }

        var method = parent.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            return false;
        }

        var parameters = method.GetParameters();
        if (parameters.Length != 1)
        {
            return false;
        }

        try
        {
            var arg = Convert.ChangeType(value, parameters[0].ParameterType);
            method.Invoke(parent, new[] { arg });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? InvokeMethodBindingValue(object root, string bindingId)
    {
        var path = bindingId.Substring("Method.".Length);
        if (!TryResolveLeaf(root, path, out var parent, out var methodName) || parent == null)
        {
            return null;
        }

        if (TryInvokeBoolMethod(parent, methodName, out var boolResult))
        {
            return boolResult;
        }

        var method = parent.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null || method.GetParameters().Length != 0)
        {
            return null;
        }

        try
        {
            return method.Invoke(parent, null);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryInvokeBoolMethod(object parent, string methodName, out bool result)
    {
        result = false;
        var method = FindParameterlessBoolMethod(parent.GetType(), methodName);
        if (method == null)
        {
            return false;
        }

        try
        {
            result = (bool)method.Invoke(parent, null)!;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryInvokePreferredBoolSetter(object parent, string fieldName, object value)
    {
        var parentType = parent.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var setters = parentType.GetMethods(flags)
            .Where(m => m.Name.StartsWith("Set", StringComparison.Ordinal) && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(bool))
            .ToList();

        foreach (var method in setters)
        {
            var suffix = method.Name.Substring(3);
            if (suffix.Length > 0 && fieldName.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TryInvokeInstanceMethod(parent, method.Name, new[] { typeof(bool) }, new object[] { Convert.ToBoolean(value) });
            }
        }

        if (setters.Count == 1)
        {
            return TryInvokeInstanceMethod(parent, setters[0].Name, new[] { typeof(bool) }, new object[] { Convert.ToBoolean(value) });
        }

        return false;
    }

    private static bool TryInvokeInstanceMethod(object parent, string methodName, Type[] paramTypes, object[] args)
    {
        var method = FindInstanceMethod(parent.GetType(), methodName, paramTypes);
        if (method == null)
        {
            return false;
        }

        try
        {
            method.Invoke(parent, args);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeMemberPath(string bindingId)
    {
        if (bindingId.StartsWith("Member.", StringComparison.Ordinal))
        {
            return bindingId.Substring("Member.".Length);
        }

        if (bindingId.StartsWith("Write.", StringComparison.Ordinal))
        {
            return bindingId.Substring("Write.".Length);
        }

        return bindingId;
    }

    private static bool TryResolveLeaf(object root, string path, out object? parent, out string leafName)
    {
        parent = root;
        leafName = string.Empty;
        var segments = path.Split('.');
        object? current = root;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment is "Aircraft" or "aircraft")
            {
                continue;
            }

            if (current == null)
            {
                return false;
            }

            if (i == segments.Length - 1)
            {
                parent = current;
                leafName = segment;
                return true;
            }

            if (!TryResolveMember(current.GetType(), segment, out var member))
            {
                return false;
            }

            current = member switch
            {
                FieldInfo fi => fi.GetValue(current),
                PropertyInfo pi => pi.GetValue(current),
                _ => null
            };
        }

        return false;
    }

    private static bool TryResolveMember(Type type, string segment, out MemberInfo? member)
    {
        var key = type.FullName + "." + segment;
        if (!MemberCache.TryGetValue(key, out member))
        {
            member = FindMember(type, segment);
            MemberCache[key] = member;
        }

        return member != null;
    }

    private static MemberInfo? FindMember(Type type, string segment)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var member = (MemberInfo?)type.GetField(segment, flags)
                     ?? type.GetProperty(segment, flags)
                     ?? (MemberInfo?)type.GetMethod(segment, flags, null, Type.EmptyTypes, null);
        if (member != null)
        {
            return member;
        }

        foreach (var field in type.GetFields(flags))
        {
            if (field.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (property.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return property;
            }
        }

        return FindParameterlessBoolMethod(type, segment);
    }

    private static MethodInfo? FindParameterlessBoolMethod(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
        if (method != null && method.ReturnType == typeof(bool))
        {
            return method;
        }

        foreach (var candidate in type.GetMethods(flags))
        {
            if (candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && candidate.GetParameters().Length == 0
                && candidate.ReturnType == typeof(bool))
            {
                return candidate;
            }
        }

        return null;
    }

    private static MethodInfo? FindInstanceMethod(Type type, string name, Type[] paramTypes)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var method = type.GetMethod(name, flags, null, paramTypes, null);
        if (method != null)
        {
            return method;
        }

        foreach (var candidate in type.GetMethods(flags))
        {
            if (!candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parameters = candidate.GetParameters();
            if (parameters.Length != paramTypes.Length)
            {
                continue;
            }

            var match = true;
            for (var i = 0; i < paramTypes.Length; i++)
            {
                if (parameters[i].ParameterType != paramTypes[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return candidate;
            }
        }

        return null;
    }
}
