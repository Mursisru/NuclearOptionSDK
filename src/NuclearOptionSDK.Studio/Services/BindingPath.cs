namespace NuclearOptionSDK.Studio.Services;

public static class BindingPath
{
    public static bool TryParseMember(string bindingId, out string typeName, out string memberName)
    {
        typeName = string.Empty;
        memberName = string.Empty;

        if (!bindingId.StartsWith("Member.", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = bindingId.Split('.');
        if (parts.Length < 3)
        {
            return false;
        }

        typeName = parts[1];
        memberName = parts[^1];
        return true;
    }

    public static bool TryParseMethod(string bindingId, out string typeName, out string methodName)
    {
        typeName = string.Empty;
        methodName = string.Empty;

        if (!bindingId.StartsWith("Method.", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = bindingId.Split('.');
        if (parts.Length < 3)
        {
            return false;
        }

        typeName = parts[1];
        methodName = parts[^1];
        return true;
    }

    public static string ToMemberAccess(string memberName) => memberName;
}
