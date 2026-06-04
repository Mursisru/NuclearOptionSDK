using Mono.Cecil;

namespace NuclearOptionSDK.Studio.Services;

public sealed class ApiTypeInfo
{
    public required string FullName { get; init; }
    public required string Namespace { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<string> Members { get; init; }
}

public static class ApiExplorerService
{
    public static IReadOnlyList<ApiTypeInfo> LoadTypes(string nuclearOptionRoot, string? filter = null)
    {
        var dllPath = Path.Combine(nuclearOptionRoot, "NuclearOption_Data", "Managed", "Assembly-CSharp.dll");
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("Assembly-CSharp.dll not found.", dllPath);
        }

        using var module = ModuleDefinition.ReadModule(dllPath);
        var list = new List<ApiTypeInfo>();

        foreach (var type in module.Types)
        {
            if (type.Name.StartsWith("<", StringComparison.Ordinal))
            {
                continue;
            }

            var fullName = type.FullName.Replace('/', '.');
            if (!string.IsNullOrWhiteSpace(filter)
                && fullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var members = new List<string>();
            foreach (var method in type.Methods)
            {
                if (method.IsConstructor)
                {
                    members.Add($".ctor({string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name))})");
                }
                else if (!method.Name.StartsWith("get_", StringComparison.Ordinal)
                         && !method.Name.StartsWith("set_", StringComparison.Ordinal))
                {
                    members.Add($"{method.ReturnType.Name} {method.Name}(...)");
                }
            }

            foreach (var field in type.Fields.Where(f => !f.IsSpecialName))
            {
                members.Add($"{field.FieldType.Name} {field.Name}");
            }

            foreach (var prop in type.Properties)
            {
                members.Add($"{prop.PropertyType.Name} {prop.Name}");
            }

            list.Add(new ApiTypeInfo
            {
                FullName = fullName,
                Namespace = type.Namespace,
                Name = type.Name,
                Members = members.OrderBy(m => m).ToList()
            });
        }

        return list.OrderBy(t => t.FullName).ToList();
    }
}
