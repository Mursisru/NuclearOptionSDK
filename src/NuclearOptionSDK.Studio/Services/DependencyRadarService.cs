using Mono.Cecil;
using Mono.Cecil.Cil;
using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

public static class DependencyRadarService
{
    public static Dictionary<string, DependencyRadarPayload> BuildIndex(string nuclearOptionRoot)
    {
        var result = new Dictionary<string, DependencyRadarPayload>(StringComparer.OrdinalIgnoreCase);
        var asmPath = Path.Combine(nuclearOptionRoot, "NuclearOption_Data", "Managed", "Assembly-CSharp.dll");
        if (!File.Exists(asmPath))
        {
            return result;
        }

        var readers = new Dictionary<string, List<DependencyNode>>(StringComparer.OrdinalIgnoreCase);
        var writers = new Dictionary<string, List<DependencyNode>>(StringComparer.OrdinalIgnoreCase);

        var assembly = AssemblyDefinition.ReadAssembly(asmPath, new ReaderParameters { ReadSymbols = false });
        foreach (var type in assembly.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                foreach (var ins in method.Body.Instructions)
                {
                    if (ins.Operand is not IMemberDefinition && ins.Operand is not MemberReference)
                    {
                        continue;
                    }

                    var memberRef = ins.Operand as MemberReference;
                    if (memberRef == null || memberRef.DeclaringType == null)
                    {
                        continue;
                    }

                    var bindingId = ApiSurface.ApiSymbolIdFactory.MemberBindingId(
                        memberRef.DeclaringType.FullName,
                        memberRef.Name);
                    var usage = ClassifyUsage(ins.OpCode.Code, memberRef.Name);
                    if (usage == null)
                    {
                        continue;
                    }

                    var node = new DependencyNode
                    {
                        typeName = type.FullName,
                        methodName = method.Name,
                        usage = usage
                    };
                    if (usage == "write")
                    {
                        AddNode(writers, bindingId, node);
                    }
                    else
                    {
                        AddNode(readers, bindingId, node);
                    }
                }
            }
        }

        foreach (var binding in readers.Keys.Concat(writers.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var payload = new DependencyRadarPayload
            {
                bindingId = binding,
                readers = readers.TryGetValue(binding, out var rs) ? rs.DistinctBy(x => $"{x.typeName}.{x.methodName}").ToArray() : Array.Empty<DependencyNode>(),
                writers = writers.TryGetValue(binding, out var ws) ? ws.DistinctBy(x => $"{x.typeName}.{x.methodName}").ToArray() : Array.Empty<DependencyNode>(),
                warnings = BuildWarnings(binding, readers, writers)
            };
            result[binding] = payload;
        }

        return result;
    }

    private static string[] BuildWarnings(
        string binding,
        IReadOnlyDictionary<string, List<DependencyNode>> readers,
        IReadOnlyDictionary<string, List<DependencyNode>> writers)
    {
        var warnings = new List<string>();
        var readerCount = readers.TryGetValue(binding, out var r) ? r.Count : 0;
        var writerCount = writers.TryGetValue(binding, out var w) ? w.Count : 0;
        if (writerCount >= 3)
        {
            warnings.Add($"Field is hot-written by {writerCount} methods; direct overwrite may conflict.");
        }

        if (readerCount >= 5)
        {
            warnings.Add($"Field is read by {readerCount} methods; changing semantics may cause side effects.");
        }

        return warnings.ToArray();
    }

    private static void AddNode(Dictionary<string, List<DependencyNode>> map, string bindingId, DependencyNode node)
    {
        if (!map.TryGetValue(bindingId, out var list))
        {
            list = new List<DependencyNode>();
            map[bindingId] = list;
        }

        list.Add(node);
    }

    private static string? ClassifyUsage(Code opcode, string memberName)
    {
        return opcode switch
        {
            Code.Ldfld or Code.Ldflda or Code.Ldind_Ref => "read",
            Code.Stfld or Code.Stind_Ref => "write",
            Code.Call or Code.Callvirt when memberName.StartsWith("get_", StringComparison.Ordinal) => "read",
            Code.Call or Code.Callvirt when memberName.StartsWith("set_", StringComparison.Ordinal) => "write",
            _ => null
        };
    }
}
