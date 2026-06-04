using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Filtering;

public interface IApiMemberFilter
{
    bool ShouldHide(ApiMemberModel member, ApiTypeModel type, ApiSurfaceRules rules);
}
