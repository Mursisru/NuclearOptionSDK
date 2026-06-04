using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Context;

public static class MemberBehaviorClassifier
{
    public static MemberBehaviorBucket Classify(ApiMemberKind kind, string clrTypeName)
    {
        if (kind is ApiMemberKind.Method or ApiMemberKind.Constructor)
        {
            return MemberBehaviorBucket.Action;
        }

        var clr = ClrTypeClassifier.Classify(clrTypeName);
        return clr switch
        {
            ClrTypeKind.Primitive or ClrTypeKind.String or ClrTypeKind.Enum => MemberBehaviorBucket.Data,
            ClrTypeKind.GameObject => MemberBehaviorBucket.Reference,
            ClrTypeKind.UnityEngine => MemberBehaviorBucket.Data,
            ClrTypeKind.Other when clrTypeName is not ("Object" or "ValueType") => MemberBehaviorBucket.Reference,
            _ => MemberBehaviorBucket.Data
        };
    }

    /// <summary>Tree folder title under each type (full words, no abbreviations).</summary>
    public static string FolderName(MemberBehaviorBucket bucket) => bucket switch
    {
        MemberBehaviorBucket.Action => "Actions",
        MemberBehaviorBucket.Data => "Data",
        MemberBehaviorBucket.Reference => "Reference",
        _ => "Other"
    };
}
