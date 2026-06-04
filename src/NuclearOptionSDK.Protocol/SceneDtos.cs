using System.Collections.Generic;

namespace NuclearOptionSDK.Protocol;

public sealed class GameObjectNode
{
    public string name { get; set; } = string.Empty;
    public int id { get; set; }
    public bool active { get; set; }
    public string[] components { get; set; } = Array.Empty<string>();
    public List<GameObjectNode> children { get; set; } = new();
}

public sealed class SceneTreePayload
{
    public string sceneName { get; set; } = string.Empty;
    public List<GameObjectNode> roots { get; set; } = new();
}

public sealed class SceneResolveRequest
{
    public int instanceId { get; set; }
}

public sealed class SceneResolveResponse
{
    public int instanceId { get; set; }
    public string name { get; set; } = string.Empty;
    public string[] components { get; set; } = Array.Empty<string>();
}
