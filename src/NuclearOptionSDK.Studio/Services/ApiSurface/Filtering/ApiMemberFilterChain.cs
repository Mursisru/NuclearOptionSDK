using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Filtering;

public sealed class ApiMemberFilterChain : IApiMemberFilter
{
    private readonly IReadOnlyList<IApiMemberFilter> _filters;

    public ApiMemberFilterChain(params IApiMemberFilter[] filters) => _filters = filters;

    public bool ShouldHide(ApiMemberModel member, ApiTypeModel type, ApiSurfaceRules rules) =>
        _filters.Any(f => f.ShouldHide(member, type, rules));
}

public sealed class CompilerNoiseFilter : IApiMemberFilter
{
    public bool ShouldHide(ApiMemberModel member, ApiTypeModel type, ApiSurfaceRules rules)
    {
        if (!rules.HideCompilerGenerated)
        {
            return false;
        }

        var name = member.TechnicalName;
        return name.StartsWith('<')
               || name.Contains("k__BackingField", StringComparison.Ordinal)
               || name.StartsWith("op_", StringComparison.Ordinal)
               || name.StartsWith("add_", StringComparison.Ordinal)
               || name.StartsWith("remove_", StringComparison.Ordinal);
    }
}

public sealed class UnityLifecycleFilter : IApiMemberFilter
{
    public bool ShouldHide(ApiMemberModel member, ApiTypeModel type, ApiSurfaceRules rules)
    {
        if (!rules.HideUnityLifecycle || member.Id.Kind != ApiMemberKind.Method)
        {
            return false;
        }

        return rules.LifecycleMethodNames.Contains(member.TechnicalName, StringComparer.Ordinal);
    }
}

public sealed class AccessorNoiseFilter : IApiMemberFilter
{
    public bool ShouldHide(ApiMemberModel member, ApiTypeModel type, ApiSurfaceRules rules)
    {
        if (!rules.HideSystemNoise)
        {
            return false;
        }

        var name = member.TechnicalName;
        return name.StartsWith("get_", StringComparison.Ordinal)
               || name.StartsWith("set_", StringComparison.Ordinal);
    }
}
