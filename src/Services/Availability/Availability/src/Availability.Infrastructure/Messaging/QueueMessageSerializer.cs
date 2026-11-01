using System.Text.Json;

namespace Availability.Infrastructure.Messaging;

/// <summary>Formato del mensaje en la cola: JSON camelCase (JsonSerializerDefaults.Web). El Base64 lo aplica el SDK.</summary>
public static class QueueMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T message) =>
        JsonSerializer.Serialize(message, message?.GetType() ?? typeof(T), Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
