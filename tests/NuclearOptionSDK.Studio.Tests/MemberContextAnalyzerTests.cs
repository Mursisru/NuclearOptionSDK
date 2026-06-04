using NuclearOptionSDK.Studio.Services.ApiSurface.Context;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;
using Xunit;

namespace NuclearOptionSDK.Studio.Tests;

public sealed class MemberContextAnalyzerTests
{
    [Fact]
    public void Method_classified_as_action_without_call_suffix_in_title()
    {
        var ctx = MemberContextAnalyzer.Analyze(
            "GameplayUI",
            ApiMemberKind.Method,
            "PauseGame",
            "Void",
            "Void PauseGame()");

        Assert.Equal(MemberBehaviorBucket.Action, ctx.Behavior);
        Assert.Equal("Actions", ctx.BehaviorFolder);
        Assert.DoesNotContain("()", ctx.FriendlyTitle);
        Assert.Contains("Pause", ctx.FriendlyTitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GameplayUI_player_is_reference_with_ui_hint()
    {
        var ctx = MemberContextAnalyzer.Analyze(
            "GameplayUI",
            ApiMemberKind.Field,
            "player",
            "Player",
            "Player player");

        Assert.Equal(MemberBehaviorBucket.Reference, ctx.Behavior);
        Assert.Equal("Reference", ctx.BehaviorFolder);
        Assert.Equal(OwnerContextKind.UserInterface, ctx.OwnerContext);
        Assert.Contains("HUD", ctx.ContextHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Player", ctx.InspectorLine);
    }

    [Fact]
    public void UI_string_field_hint_mentions_text()
    {
        var ctx = MemberContextAnalyzer.Analyze(
            "CombatHUD",
            ApiMemberKind.Property,
            "playerName",
            "String",
            "String playerName");

        Assert.Equal(MemberBehaviorBucket.Data, ctx.Behavior);
        Assert.Contains("text", ctx.ContextHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Network_player_reference_hint()
    {
        var ctx = MemberContextAnalyzer.Analyze(
            "NetworkPlayerSync",
            ApiMemberKind.Field,
            "remotePlayer",
            "Player",
            "Player remotePlayer");

        Assert.Equal(OwnerContextKind.Network, ctx.OwnerContext);
        Assert.Contains("Network", ctx.ContextHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MemberBehaviorClassifier_splits_primitive_vs_reference()
    {
        Assert.Equal(MemberBehaviorBucket.Action,
            MemberBehaviorClassifier.Classify(ApiMemberKind.Method, "Void"));
        Assert.Equal(MemberBehaviorBucket.Data,
            MemberBehaviorClassifier.Classify(ApiMemberKind.Field, "Single"));
        Assert.Equal(MemberBehaviorBucket.Reference,
            MemberBehaviorClassifier.Classify(ApiMemberKind.Field, "Player"));
    }

    [Fact]
    public void FolderName_uses_full_words_not_abbreviations()
    {
        Assert.Equal("Actions", MemberBehaviorClassifier.FolderName(MemberBehaviorBucket.Action));
        Assert.Equal("Data", MemberBehaviorClassifier.FolderName(MemberBehaviorBucket.Data));
        Assert.Equal("Reference", MemberBehaviorClassifier.FolderName(MemberBehaviorBucket.Reference));
    }
}
