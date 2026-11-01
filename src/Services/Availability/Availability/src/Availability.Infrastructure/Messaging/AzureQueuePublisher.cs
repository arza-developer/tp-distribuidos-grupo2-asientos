using System.Collections.Concurrent;
using Availability.Application.Exceptions;
using Azure.Core;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeatsReservation.Contracts.Events;

namespace Availability.Infrastructure.Messaging;

/// <summary>
/// Publica en Azure Queue (Azurite en desarrollo). Singleton: cachea un QueueClient por cola y crea la cola
/// una sola vez. Todo error del SDK se envuelve en <see cref="MessagePublishException"/> (fallo técnico → 503),
/// para que Application no conozca tipos de Azure.
/// </summary>
public sealed class AzureQueuePublisher(IOptions<AzureQueueOptions> options, ILogger<AzureQueuePublisher> logger)
    : IMessagePublisher
{
    private readonly AzureQueueOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, QueueClient> _clients = new();
    private readonly ConcurrentDictionary<string, bool> _ensured = new();

    public async Task PublishAsync<T>(T message, string topicOrQueue, CancellationToken ct = default)
    {
        try
        {
            var client = _clients.GetOrAdd(topicOrQueue, CreateClient);

            if (!_ensured.ContainsKey(topicOrQueue))
            {
                await client.CreateIfNotExistsAsync(cancellationToken: ct);
                _ensured[topicOrQueue] = true;
            }

            await client.SendMessageAsync(QueueMessageSerializer.Serialize(message), ct);
            logger.LogInformation("Mensaje {MessageType} publicado en la cola '{Queue}'", typeof(T).Name, topicOrQueue);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo publicar {MessageType} en la cola '{Queue}'", typeof(T).Name, topicOrQueue);
            throw new MessagePublishException($"No se pudo publicar en la cola '{topicOrQueue}'.", ex);
        }
    }

    private QueueClient CreateClient(string queueName)
    {
        var clientOptions = new QueueClientOptions
        {
            // Base64: compatible con los consumidores estándar de Azure Queue.
            MessageEncoding = QueueMessageEncoding.Base64,
            Retry =
            {
                MaxRetries = _options.MaxRetries,
                Mode = RetryMode.Fixed,
                Delay = TimeSpan.FromMilliseconds(200),
                NetworkTimeout = TimeSpan.FromSeconds(_options.NetworkTimeoutSeconds)
            }
        };
        return new QueueClient(_options.ConnectionString, queueName, clientOptions);
    }
}
