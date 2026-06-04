using NuclearOptionSDK.Decompiler;
using Xunit;

namespace NuclearOptionSDK.Decompiler.Tests;

public sealed class PreviewTextPolicyTests
{
    [Fact]
    public void Empty_is_weak()
    {
        Assert.True(PreviewTextPolicy.IsWeakPreview(null));
        Assert.True(PreviewTextPolicy.IsWeakPreview("   "));
    }

    [Fact]
    public void Signature_only_is_weak()
    {
        const string sig = "void Foo(int x);";
        Assert.True(PreviewTextPolicy.IsWeakPreview(sig, sig));
    }

    [Fact]
    public void Multi_line_body_is_strong()
    {
        var text = """
            public void Foo()
            {
                var x = 1;
                var y = x + 2;
                return;
            }
            """;
        Assert.False(PreviewTextPolicy.IsWeakPreview(text));
    }

    [Fact]
    public void Rva_stub_without_brace_is_weak()
    {
        Assert.True(PreviewTextPolicy.IsWeakPreview("// RVA: 0x1234\nvoid Foo();"));
    }
}
