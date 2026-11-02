using System.Collections.Concurrent;
using Availability.Application.Exceptions;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeatsReservation.Contracts.Events;

namespace Availability.Infrastructure.Messaging;

/// <summary>
/// Publica en un topic de Azure Service Bus (el que consume Booking). Singleton: crea el <see cref="ServiceBusClient"/>
/// de forma perezosa (sin connection string la app arranca y el error aparece al publicar) y cachea un sender por topic.
/// Todo error del SDK se envuelve en <see cref="MessagePublishException"/> (fallo técnico → 503), para que Application
/// no conozca tipos de Azure. El cuerpo es JSON camelCase (<see cref="QueueMessageSerializer"/>), igual que lo lee Booking.
/// </summary>
public sealed class ServiceBusPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ILogger<ServiceBusPublisher> _logger;
    private readonly Func<string, ServiceBusSender> _senderFactory;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
    private readonly Lazy<ServiceBusClient>? _client;

    public ServiceBusPublisher(IOptions<ServiceBusOptions> options, ILogger<ServiceBusPublisher> logger)
    {
        _logger = logger;
        var o = options.Value;
        _client = new Lazy<ServiceBusClient>(() => new ServiceBusClient(o.ConnectionString, new ServiceBusClientOptions
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                MaxRetries = o.MaxRetries,
                Mode = ServiceBusRetryMode.Fixed,
                Delay = TimeSpan.FromMilliseconds(200),
                TryTimeout = TimeSpan.FromSeconds(o.TryTimeoutSeconds)
            }
        }));
        _senderFactory = topic => _client.Value.CreateSender(topic);
    }

    /// <summary>Constructor para pruebas: permite inyectar un sender falso sin tocar la red.</summary>
    public ServiceBusPublisher(Func<string, ServiceBusSender> senderFactory, ILogger<ServiceBusPublisher> logger)
    {
        _senderFactory = senderFactory;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T message, string topicOrQueue, CancellationToken ct = default)
    {
        try
        {
            var sender = _senders.GetOrAdd(topicOrQueue, _senderFactory);
            await sender.SendMessageAsync(ToServiceBusMessage(message), ct);
            _logger.LogInformation("Mensaje {MessageType} publicado en el topic '{Topic}'", typeof(T).Name, topicOrQueue);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo publicar {MessageType} en el topic '{Topic}'", typeof(T).Name, topicOrQueue);
            throw new MessagePublishException($"No se pudo publicar en el topic '{topicOrQueue}'.", ex);
        }
    }

    internal static ServiceBusMessage ToServiceBusMessage<T>(T message)
    {
        var sbMessage = new ServiceBusMessage(QueueMessageSerializer.Serialize(message)) { ContentType = "application/json" };
        if (message is ReservationRequestedEvent evt)
        {
            sbMessage.MessageId = evt.ReservationId.ToString(); // permite deduplicar reintentos si el topic lo habilita
            sbMessage.CorrelationId = evt.CorrelationId;        // trazabilidad entre servicios (req. 3.f)
            sbMessage.ApplicationProperties["CorrelationId"] = evt.CorrelationId;
        }
        return sbMessage;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values) await sender.DisposeAsync();
        if (_client is { IsValueCreated: true }) await _client.Value.DisposeAsync();
    }
}
