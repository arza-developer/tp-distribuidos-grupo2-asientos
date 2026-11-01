namespace Availability.Infrastructure.Messaging;

public class AzureQueueOptions
{
    public const string SectionName = "AzureQueue";

    /// <summary>Por defecto Azurite local (emulador de Azure Storage).</summary>
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";

    /// <summary>Reintentos del SDK. Bajo a propósito: si el broker cae, la API debe fallar rápido (503).</summary>
    public int MaxRetries { get; set; } = 1;

    public int NetworkTimeoutSeconds { get; set; } = 5;
}
