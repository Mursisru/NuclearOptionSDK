using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;

public static class SymbolLabelService
{
    private static readonly Lazy<ILabelResolver> Resolver = new(CreateResolver);

    public static string ForType(string typeFullName, string? displayTag = null)
    {
        if (Resolver.Value.TryResolveType(typeFullName, out var label))
        {
            var tag = displayTag ?? label.Tag;
            return string.IsNullOrEmpty(tag) ? label.Title : $"[{tag}] {label.Title}";
        }

        return typeFullName.Contains('.')
            ? typeFullName[(typeFullName.LastIndexOf('.') + 1)..]
            : typeFullName;
    }

    public static string ForMember(string typeFullName, string memberName, string? signature = null)
    {
        if (Resolver.Value.TryResolveMember(typeFullName, memberName, out var label))
        {
            return label.Title;
        }

        return memberName;
    }

    public static string? HintForMember(string typeFullName, string memberName)
    {
        if (Resolver.Value.TryResolveMember(typeFullName, memberName, out var label))
        {
            return label.Hint;
        }

        return null;
    }

    public static string CollisionBadge(ApiMemberModel member)
    {
        if (member.Source == ApiMemberSource.Inherited && !string.IsNullOrEmpty(member.DeclaringBaseType))
        {
            return $" · base {ApiSymbolIdFactory.ShortTypeName(member.DeclaringBaseType)}";
        }

        if (member.Source == ApiMemberSource.Composed && !string.IsNullOrEmpty(member.Id.CompositionPath))
        {
            return $" · {member.Id.CompositionPath}";
        }

        return string.Empty;
    }

    public static string InspectorLine(ApiMemberModel member) =>
        $"{member.Id.Kind.ToString().ToLowerInvariant()} · {member.ClrTypeName}{CollisionBadge(member)}";

    private static ILabelResolver CreateResolver()
    {
        var store = JsonSymbolLabelStore.LoadDefault();
        return new LabelResolverChain(
            new JsonLabelResolver(store),
            new HumanizeLabelResolver());
    }
}
