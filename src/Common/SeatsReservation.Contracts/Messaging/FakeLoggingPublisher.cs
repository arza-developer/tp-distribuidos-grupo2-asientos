using Microsoft.Extensions.Logging;
using SeatsReservation.Contracts.Events;

namespace SeatsReservation.Contracts.Messaging;

public class FakeLoggingPublisher : IMessagePublisher
{
    private readonly ILogger<FakeLoggingPublisher> _logger;

    public FakeLoggingPublisher(ILogger<FakeLoggingPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T message, string topicOrQueue, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK BROKER/AZURE] Publicando evento en '{Topic}': {@Message}", topicOrQueue, message);
        return Task.CompletedTask;
    }
}

