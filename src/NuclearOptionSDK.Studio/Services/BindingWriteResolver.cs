namespace NuclearOptionSDK.Studio.Services;

/// <summary>Maps Member.* write bindings to Method.* when the game exposes a bool setter (e.g. SetGear).</summary>
public static class BindingWriteResolver
{
    public static string ResolveWriteBindingId(string bindingId)
    {
        if (string.IsNullOrWhiteSpace(bindingId))
        {
            return bindingId;
        }

        if (bindingId.StartsWith("Method.", StringComparison.Ordinal))
        {
            return bindingId;
        }

        if (!BindingPath.TryParseMember(bindingId, out var typeName, out var memberName))
        {
            return bindingId;
        }

        if (TryFindBoolSetterBinding(typeName, memberName, out var setterBinding))
        {
            return setterBinding;
        }

        return bindingId;
    }

    private static bool TryFindBoolSetterBinding(string typeName, string fieldName, out string bindingId)
    {
        bindingId = string.Empty;
        var type = GameCodeIndexCache.FindType(typeName);
        if (type == null)
        {
            return false;
        }

        var setters = type.Methods
            .Where(m => m.Name.StartsWith("Set", StringComparison.Ordinal) && LooksLikeBoolSetter(m))
            .ToList();

        foreach (var method in setters)
        {
            var suffix = method.Name.Substring(3);
            if (suffix.Length > 0 && fieldName.Contains(suffix, StringComparison.OrdinalIgnoreCase))
            {
                bindingId = method.BindingId;
                return true;
            }
        }

        if (setters.Count == 1)
        {
            bindingId = setters[0].BindingId;
            return true;
        }

        return false;
    }

    private static bool LooksLikeBoolSetter(GameMemberNode method) =>
        method.Signature.Contains("bool", StringComparison.OrdinalIgnoreCase)
        || method.Signature.Contains("Boolean", StringComparison.OrdinalIgnoreCase);
}
