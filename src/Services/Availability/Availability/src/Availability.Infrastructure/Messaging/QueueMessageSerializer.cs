using System.Text.Json;

namespace Availability.Infrastructure.Messaging;

/// <summary>Formato del cuerpo del mensaje: JSON camelCase (JsonSerializerDefaults.Web), que es el que lee Booking.</summary>
public static class QueueMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T message) =>
        JsonSerializer.Serialize(message, message?.GetType() ?? typeof(T), Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
