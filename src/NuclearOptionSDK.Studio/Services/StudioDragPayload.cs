namespace NuclearOptionSDK.Studio.Services;

/// <summary>Безопасная сериализация drag-payload (в displayName могут быть символы |).</summary>
public static class StudioDragPayload
{
    private const char Sep = '\u001e';

    public static string Encode(params string[] parts) => string.Join(Sep, parts);

    public static string[] Decode(string? payload) =>
        string.IsNullOrEmpty(payload) ? Array.Empty<string>() : payload.Split(Sep);
}
