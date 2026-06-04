using NuclearOptionSDK.Studio.Services.ApiSurface.Labeling;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;

namespace NuclearOptionSDK.Studio.Services.ApiSurface.Context;

public sealed record MemberContextInfo(
    MemberBehaviorBucket Behavior,
    string BehaviorFolder,
    string FriendlyTitle,
    string ContextHint,
    string InspectorLine,
    OwnerContextKind OwnerContext,
    ClrTypeKind ValueTypeKind);

public static class MemberContextAnalyzer
{
    public static MemberContextInfo Analyze(
        string ownerTypeFullName,
        ApiMemberKind memberKind,
        string memberName,
        string clrTypeName,
        string signature)
    {
        var ownerShort = ApiSymbolIdFactory.ShortTypeName(ownerTypeFullName);
        var ownerCtx = OwnerContextClassifier.ClassifyType(ownerShort);
        var ownerLabel = OwnerContextClassifier.OwnerLabel(ownerCtx);
        var behavior = MemberBehaviorClassifier.Classify(memberKind, clrTypeName);
        var folder = MemberBehaviorClassifier.FolderName(behavior);
        var clrKind = ClrTypeClassifier.Classify(clrTypeName);

        var jsonHint = SymbolLabelService.HintForMember(ownerTypeFullName, memberName);
        var friendly = SymbolLabelService.ForMember(ownerTypeFullName, memberName, signature);
        var contextHint = jsonHint ?? InferContextHint(ownerCtx, ownerShort, memberName, clrTypeName, clrKind, behavior, memberKind);
        var title = friendly;

        var access = memberKind is ApiMemberKind.Property ? "Property" : memberKind.ToString();
        var inspector = behavior switch
        {
            MemberBehaviorBucket.Action =>
                $"[{ownerLabel}] {ownerShort} → action {memberName}() : {clrTypeName}",
            MemberBehaviorBucket.Reference =>
                $"[{ownerLabel}] {ownerShort} → reference to {clrTypeName} ({memberName}) | {access}",
            _ =>
                $"[{ownerLabel}] {ownerShort} → {memberName} : {clrTypeName} | {access}"
        };

        return new MemberContextInfo(
            behavior,
            folder,
            title,
            contextHint,
            inspector,
            ownerCtx,
            clrKind);
    }

    private static string InferContextHint(
        OwnerContextKind owner,
        string ownerShort,
        string memberName,
        string clrType,
        ClrTypeKind clrKind,
        MemberBehaviorBucket behavior,
        ApiMemberKind kind)
    {
        if (behavior == MemberBehaviorBucket.Action)
        {
            return $"Invokes game logic on {ownerShort}; returns {clrType}.";
        }

        if (owner == OwnerContextKind.UserInterface && clrKind == ClrTypeKind.GameObject)
        {
            return $"Link to {clrType} for HUD/UI binding (e.g. pilot stats, labels). Owner: {ownerShort}.";
        }

        if (owner == OwnerContextKind.UserInterface && clrKind == ClrTypeKind.String)
        {
            return $"UI text field — likely label or name shown on screen ({memberName}).";
        }

        if (owner == OwnerContextKind.UserInterface && clrKind == ClrTypeKind.Primitive)
        {
            return $"UI-driven scalar value displayed or edited on HUD ({clrType}).";
        }

        if (owner == OwnerContextKind.Network && clrKind == ClrTypeKind.GameObject)
        {
            return $"Network/session handle to remote or local {clrType}.";
        }

        if (owner == OwnerContextKind.Flight && clrKind == ClrTypeKind.GameObject)
        {
            return $"Reference to flyable or tracked {clrType} instance.";
        }

        if (behavior == MemberBehaviorBucket.Reference)
        {
            return $"Stores a reference to {clrType} ({memberName}) on {ownerShort}.";
        }

        if (clrKind == ClrTypeKind.Primitive || clrKind == ClrTypeKind.String)
        {
            return $"Readable/writable {clrType} field on {ownerShort}.";
        }

        return $"{kind} {memberName} : {clrType} on {ownerShort}.";
    }
}
