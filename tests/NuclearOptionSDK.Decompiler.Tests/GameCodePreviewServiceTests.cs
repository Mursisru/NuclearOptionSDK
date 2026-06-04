using NuclearOptionSDK.Studio.Services;
using NuclearOptionSDK.Studio.Services.ApiSurface.Models;
using Xunit;

namespace NuclearOptionSDK.Decompiler.Tests;

public sealed class GameCodePreviewServiceTests
{
    private sealed class FakeDecompile : Decompiler.IDecompileService
    {
        public Task<string?> DecompileMethodAsync(
            string? nuclearOptionRoot,
            string typeFullName,
            string methodName,
            bool bypassCache = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>($"// decompiled {typeFullName}.{methodName}");

        public Task<string?> DecompileTypeAsync(
            string? nuclearOptionRoot,
            string typeFullName,
            bool bypassCache = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>($"// type {typeFullName}");
    }

    [Fact]
    public async Task Weak_preview_uses_decompiler()
    {
        var svc = new GameCodePreviewService(new FakeDecompile());
        var member = new GameMemberNode
        {
            Behavior = MemberBehaviorBucket.Action,
            Kind = GameMemberKind.Method,
            Name = "Update",
            Signature = "void Update();",
            BindingId = "Member.Aircraft.Update",
            PreviewText = null
        };
        var owner = new GameTypeNode
        {
            FullName = "Aircraft",
            ShortName = "Aircraft",
            Parameters = Array.Empty<GameMemberNode>(),
            Methods = new[] { member },
            Values = Array.Empty<GameMemberNode>()
        };

        var text = await svc.ResolvePreviewAsync(member, owner, @"C:\fake");
        Assert.Contains("decompiled Aircraft.Update", text);
    }

    [Fact]
    public async Task Non_method_returns_signature_only()
    {
        var svc = new GameCodePreviewService(new FakeDecompile());
        var member = new GameMemberNode
        {
            Behavior = MemberBehaviorBucket.Data,
            Kind = GameMemberKind.Parameter,
            Name = "speed",
            Signature = "float speed;",
            BindingId = "Member.Aircraft.speed",
            PreviewText = null
        };

        var text = await svc.ResolvePreviewAsync(member, null, @"C:\fake");
        Assert.Equal("float speed;", text);
    }
}
