using SeatsReservation.Contracts.Events;

namespace Availability.Tests.Api;

/// <summary>Fake del broker para tests de API: registra lo publicado y puede fallar a pedido.</summary>
public sealed class RecordingMessagePublisher : IMessagePublisher
{
    public List<(object Message, string Queue)> Published { get; } = [];
    public Exception? ThrowOnPublish { get; set; }

    public Task PublishAsync<T>(T message, string topicOrQueue, CancellationToken ct = default)
    {
        if (ThrowOnPublish is not null) throw ThrowOnPublish;
        Published.Add((message!, topicOrQueue));
        return Task.CompletedTask;
    }

    public void Reset()
    {
        Published.Clear();
        ThrowOnPublish = null;
    }
}
