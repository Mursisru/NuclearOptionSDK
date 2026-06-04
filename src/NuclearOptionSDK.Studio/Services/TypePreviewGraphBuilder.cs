using NuclearOptionSDK.Protocol;

namespace NuclearOptionSDK.Studio.Services;

/// <summary>Reference preview for a whole game type (folder row or type node in «Код игры»).</summary>
public static class TypePreviewGraphBuilder
{
    public static LogicGraph Build(GameTypeNode type)
    {
        var method = type.Methods
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.PreviewText))
            ?? type.Methods.FirstOrDefault();

        if (method != null)
        {
            return MethodPreviewGraphBuilder.Build(method);
        }

        var bindingId = $"Member.{type.ShortName}";
        return ReferenceGraphLayout.PackHorizontalChain(new LogicGraph
        {
            nodes =
            [
                new LogicNode
                {
                    id = "n0",
                    kind = "source",
                    typeId = "Member.Bind",
                    x = 40,
                    y = 80,
                    parameters = new Dictionary<string, string>
                    {
                        ["bindingId"] = bindingId,
                        ["displayName"] = type.ShortName
                    }
                }
            ],
            edges = Array.Empty<LogicEdge>()
        }, 80f);
    }
}
