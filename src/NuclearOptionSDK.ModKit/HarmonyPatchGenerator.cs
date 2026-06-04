using System.Text;
using NuclearOptionSDK.Protocol;

using System;

namespace NuclearOptionSDK.ModKit;

public static class HarmonyPatchGenerator
{
    public static string Generate(HarmonyGenerateRequest request)
    {
        var patchMethod = request.patchKind.Equals("Postfix", StringComparison.OrdinalIgnoreCase)
            ? "Postfix"
            : "Prefix";

        var sb = new StringBuilder();
        sb.AppendLine("using HarmonyLib;");
        sb.AppendLine();
        sb.AppendLine($"namespace {request.modNamespace};");
        sb.AppendLine();
        sb.AppendLine($"[HarmonyPatch(typeof({request.targetType}), \"{request.methodName}\")]");
        sb.AppendLine($"public static class {request.className}");
        sb.AppendLine("{");
        sb.AppendLine($"    static void {patchMethod}()");
        sb.AppendLine("    {");
        sb.AppendLine("        // TODO: add patch logic");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
