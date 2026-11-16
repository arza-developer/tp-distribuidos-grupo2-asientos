namespace Availability.Infrastructure.Messaging;

public class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    /// <summary>
    /// Connection string del namespace de Azure Service Bus (secreto: user-secrets o variable de entorno
    /// <c>ServiceBus__ConnectionString</c>, nunca en archivos versionados). Vacía → el arranque no falla; publicar responde 503.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Reintentos del SDK. Bajo a propósito: si el broker cae, la API debe fallar rápido (503).</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>Tiempo máximo de cada intento de envío.</summary>
    public int TryTimeoutSeconds { get; set; } = 5;
}
