namespace SeatsReservation.Contracts.Events;

public record ReservationRequestedEvent
{
    public Guid ReservationId { get; init; } = Guid.NewGuid();
    public Guid EventId { get; init; }
    public string SeatNumber { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public decimal Price { get; init; }
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
}

public record ReservationConfirmedEvent
{
    public Guid ReservationId { get; init; }
    public Guid UserId { get; init; }
    public string SeatNumber { get; init; } = string.Empty;
    public string TransactionId { get; init; } = string.Empty;
    public DateTime ConfirmedAtUtc { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
}

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string topicOrQueue, CancellationToken ct = default);
}

