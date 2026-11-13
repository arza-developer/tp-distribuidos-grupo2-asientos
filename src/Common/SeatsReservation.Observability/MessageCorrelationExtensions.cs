namespace SeatsReservation.Observability;

public static class MessageCorrelationExtensions
{
    private const string CorrelationIdPropertyName = "CorrelationId";

    public static void InjectCorrelationId(IDictionary<string, object> properties, string correlationId)
    {
        if (properties.ContainsKey(CorrelationIdPropertyName))
        {
            properties[CorrelationIdPropertyName] = correlationId;
        }
        else
        {
            properties.Add(CorrelationIdPropertyName, correlationId);
        }
    }

    public static string ExtractCorrelationId(IDictionary<string, object> properties)
    {
        if (properties != null && properties.TryGetValue(CorrelationIdPropertyName, out var value) && value is string correlationId)
        {
            return correlationId;
        }

        return Guid.NewGuid().ToString();
    }
}