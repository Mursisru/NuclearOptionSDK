using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuclearOptionSDK.Protocol;

public static class ProtocolJson
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None
    };

    public static string Serialize(MessageEnvelope envelope)
    {
        return JsonConvert.SerializeObject(envelope, Settings);
    }

    public static MessageEnvelope Deserialize(string json)
    {
        return JsonConvert.DeserializeObject<MessageEnvelope>(json, Settings)
               ?? throw new JsonException("Invalid envelope.");
    }

    public static T Payload<T>(MessageEnvelope envelope)
    {
        if (envelope.payload == null)
        {
            return Activator.CreateInstance<T>();
        }

        if (envelope.payload is T typed)
        {
            return typed;
        }

        if (envelope.payload is JObject jobj)
        {
            return jobj.ToObject<T>() ?? Activator.CreateInstance<T>();
        }

        return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(envelope.payload), Settings)
               ?? Activator.CreateInstance<T>();
    }

    public static MessageEnvelope Create(string type, object? payload = null, string? id = null)
    {
        return new MessageEnvelope
        {
            v = ProtocolVersion.Current,
            type = type,
            id = id ?? Guid.NewGuid().ToString("N"),
            payload = payload
        };
    }
}
