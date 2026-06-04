using Mono.Cecil;
using NuclearOptionSDK.Studio.Services.ApiSurface.Context;
using NuclearOptionSDK.Studio.Services.ApiSurface.Filtering;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;
using NuclearOptionSDK.Studio.Services.ApiSurface.Taxonomy;

namespace NuclearOptionSDK.Studio.Services.ApiSurface;

public static class CecilMetadataReader
{
    public static string ResolveDllPath(string nuclearOptionRoot) =>
        Path.Combine(nuclearOptionRoot, "NuclearOption_Data", "Managed", "Assembly-CSharp.dll");

    public static IReadOnlyList<ApiTypeModel> ReadTypes(string nuclearOptionRoot, ApiSurfaceRules rules, string? filter = null)
    {
        var dllPath = ResolveDllPath(nuclearOptionRoot);
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("Assembly-CSharp.dll not found.", dllPath);
        }

        using var module = ModuleDefinition.ReadModule(dllPath);
        var filterChain = new ApiMemberFilterChain(
            new CompilerNoiseFilter(),
            new UnityLifecycleFilter(),
            new AccessorNoiseFilter());

        var list = new List<ApiTypeModel>();
        foreach (var type in module.Types)
        {
            if (type.Name.StartsWith('<'))
            {
                continue;
            }

            var fullName = type.FullName.Replace('/', '.');
            if (!string.IsNullOrWhiteSpace(filter)
                && fullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                && type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var category = KeywordCategoryAssigner.ForType(type.Name);
            var tag = KeywordCategoryAssigner.TypeTag(type.Name);
            var priority = ScoreType(type, rules);
            var members = BuildMembers(type, fullName, category, rules, filterChain);
            if (members.Count == 0 && !string.IsNullOrWhiteSpace(filter))
            {
                continue;
            }

            list.Add(new ApiTypeModel
            {
                FullName = fullName,
                Namespace = type.Namespace,
                Name = type.Name,
                BaseTypeFullName = type.BaseType?.FullName?.Replace('/', '.'),
                IsEnum = type.IsEnum,
                PriorityScore = priority,
                DisplayTag = tag,
                Category = category,
                Members = members
            });
        }

        return list
            .OrderByDescending(t => t.PriorityScore)
            .ThenBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ScoreType(TypeDefinition type, ApiSurfaceRules rules)
    {
        var score = string.IsNullOrEmpty(type.Namespace) ? 50 : 0;
        if (type.Namespace?.StartsWith("NuclearOption", StringComparison.Ordinal) == true)
        {
            score += 40;
        }

        foreach (var boost in rules.TypePriorityBoost)
        {
            if (string.Equals(type.Name, boost, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
        }

        return score;
    }

    private static List<ApiMemberModel> BuildMembers(
        TypeDefinition type,
        string fullName,
        ApiSurfaceCategory typeCategory,
        ApiSurfaceRules rules,
        ApiMemberFilterChain filterChain)
    {
        var members = new List<ApiMemberModel>();

        foreach (var field in type.Fields.Where(f => !f.IsSpecialName))
        {
            members.Add(CreateMember(
                fullName,
                ApiMemberKind.Field,
                field.Name,
                $"{field.FieldType.Name} {field.Name}",
                field.FieldType.Name,
                ApiMemberSource.Declared,
                typeCategory,
                rules,
                filterChain,
                null));
        }

        foreach (var prop in type.Properties)
        {
            members.Add(CreateMember(
                fullName,
                ApiMemberKind.Property,
                prop.Name,
                $"{prop.PropertyType.Name} {prop.Name}",
                prop.PropertyType.Name,
                ApiMemberSource.Declared,
                typeCategory,
                rules,
                filterChain,
                null));
        }

        foreach (var method in type.Methods)
        {
            if (method.IsConstructor)
            {
                var sig = $".ctor({string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name))})";
                members.Add(CreateMember(
                    fullName,
                    ApiMemberKind.Constructor,
                    ".ctor",
                    sig,
                    "void",
                    ApiMemberSource.Declared,
                    typeCategory,
                    rules,
                    filterChain,
                    null));
                continue;
            }

            if (method.Name.StartsWith("get_", StringComparison.Ordinal)
                || method.Name.StartsWith("set_", StringComparison.Ordinal))
            {
                continue;
            }

            var methodSig = FormatMethodSignature(method);
            members.Add(CreateMember(
                fullName,
                ApiMemberKind.Method,
                method.Name,
                methodSig,
                method.ReturnType.Name,
                ApiMemberSource.Declared,
                typeCategory,
                rules,
                filterChain,
                null));
        }

        return members
            .Where(m => !m.IsHidden)
            .OrderByDescending(m => m.PriorityScore)
            .ThenBy(m => m.TechnicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ApiMemberModel CreateMember(
        string typeFullName,
        ApiMemberKind kind,
        string name,
        string signature,
        string clrType,
        ApiMemberSource source,
        ApiSurfaceCategory typeCategory,
        ApiSurfaceRules rules,
        ApiMemberFilterChain filterChain,
        string? baseType)
    {
        var id = new ApiSymbolId(typeFullName, kind, name);
        var bindingId = ApiSymbolIdFactory.MemberBindingId(id);
        var category = KeywordCategoryAssigner.ForMember(name, typeCategory);
        var clrKind = ClrTypeClassifier.Classify(clrType);
        var behavior = MemberBehaviorClassifier.Classify(kind, clrType);
        var model = new ApiMemberModel
        {
            Id = id,
            TechnicalName = name,
            Signature = signature,
            ClrTypeName = clrType,
            ClrKind = clrKind,
            Behavior = behavior,
            Source = source,
            Category = category,
            IsHidden = false,
            PriorityScore = 0,
            DeclaringBaseType = baseType,
            BindingId = bindingId
        };

        var stubType = new ApiTypeModel
        {
            FullName = typeFullName,
            Namespace = string.Empty,
            Name = ApiSymbolIdFactory.ShortTypeName(typeFullName),
            BaseTypeFullName = baseType,
            IsEnum = false,
            PriorityScore = 0,
            DisplayTag = null,
            Category = typeCategory,
            Members = Array.Empty<ApiMemberModel>()
        };
        var hidden = filterChain.ShouldHide(model, stubType, rules);
        model.IsHidden = hidden;
        return model;
    }

    private static string FormatMethodSignature(MethodDefinition method)
    {
        var args = string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name));
        var ret = method.ReturnType.Name;
        return $"{ret} {method.Name}({args})";
    }
}
