namespace NuclearOptionSDK.Studio.Services;

/// <summary>
/// Holds the last dragged game member (preview can be large — not in drag payload string).
/// </summary>
public static class GameCodeDragContext
{
    public static GameMemberNode? LastDraggedMember { get; set; }

    public static GameTypeNode? LastDraggedType { get; set; }

    public static void Clear()
    {
        LastDraggedMember = null;
        LastDraggedType = null;
    }
}
