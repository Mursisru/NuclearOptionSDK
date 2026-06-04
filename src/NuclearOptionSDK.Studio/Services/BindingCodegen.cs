using System.Text;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Emits direct game API calls for logic mods (compile-time), with reflection fallback.</summary>
public static class BindingCodegen
{
    public const string AircraftVariable = "aircraft";

    public static bool PreferTypedAccess => true;

    public static void EmitLocalAircraftGuard(StringBuilder sb, string indent)
    {
        sb.AppendLine($"{indent}if (!GameManager.GetLocalAircraft(out Aircraft {AircraftVariable}))");
        sb.AppendLine($"{indent}    return;");
    }

    public static void EmitReadBool(StringBuilder sb, string bindingId, string indent, string valueVar)
    {
        if (TryEmitTypedRead(sb, bindingId, indent, valueVar, "bool"))
        {
            return;
        }

        sb.AppendLine(
            $"{indent}bool {valueVar} = GameBindingRuntime.ReadBool({AircraftVariable}, \"{Escape(bindingId)}\");");
    }

    public static void EmitReadFloat(StringBuilder sb, string bindingId, string indent, string valueVar)
    {
        if (TryEmitTypedRead(sb, bindingId, indent, valueVar, "float"))
        {
            return;
        }

        sb.AppendLine(
            $"{indent}float {valueVar} = GameBindingRuntime.ReadFloat({AircraftVariable}, \"{Escape(bindingId)}\");");
    }

    public static void EmitWrite(StringBuilder sb, string bindingId, string valueLiteral, string indent)
    {
        var effective = BindingWriteResolver.ResolveWriteBindingId(bindingId);
        if (TryEmitTypedWrite(sb, effective, valueLiteral, indent))
        {
            return;
        }

        sb.AppendLine(
            $"{indent}GameBindingRuntime.ApplyWrite({AircraftVariable}, \"{Escape(bindingId)}\", {valueLiteral});");
    }

    private static bool TryEmitTypedRead(
        StringBuilder sb,
        string bindingId,
        string indent,
        string valueVar,
        string valueKind)
    {
        if (!PreferTypedAccess)
        {
            return false;
        }

        if (BindingPath.TryParseMethod(bindingId, out _, out var methodName))
        {
            sb.AppendLine($"{indent}{valueKind} {valueVar} = {AircraftVariable}.{methodName}();");
            return true;
        }

        if (!BindingPath.TryParseMember(bindingId, out _, out var memberName))
        {
            return false;
        }

        if (GameCodeIndexCache.TryGetMember(bindingId, out var member)
            && member.Kind == GameMemberKind.Method)
        {
            sb.AppendLine($"{indent}{valueKind} {valueVar} = {AircraftVariable}.{memberName}();");
            return true;
        }

        sb.AppendLine($"{indent}{valueKind} {valueVar} = {AircraftVariable}.{BindingPath.ToMemberAccess(memberName)};");
        return true;
    }

    private static bool TryEmitTypedWrite(StringBuilder sb, string bindingId, string valueLiteral, string indent)
    {
        if (!PreferTypedAccess)
        {
            return false;
        }

        if (BindingPath.TryParseMethod(bindingId, out _, out var methodName))
        {
            sb.AppendLine($"{indent}{AircraftVariable}.{methodName}({valueLiteral});");
            return true;
        }

        if (!BindingPath.TryParseMember(bindingId, out _, out var memberName))
        {
            return false;
        }

        if (GameCodeIndexCache.TryGetMember(bindingId, out var member)
            && member.Kind == GameMemberKind.Method)
        {
            sb.AppendLine($"{indent}{AircraftVariable}.{memberName}({valueLiteral});");
            return true;
        }

        sb.AppendLine($"{indent}{AircraftVariable}.{BindingPath.ToMemberAccess(memberName)} = {valueLiteral};");
        return true;
    }

    private static string Escape(string bindingId) => bindingId.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
