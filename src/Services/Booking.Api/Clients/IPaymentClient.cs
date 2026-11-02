namespace Booking.Api.Clients;

public enum PaymentOutcome
{
    Confirmed,
    Failed,
    Unavailable
}

public record PaymentResult(PaymentOutcome Outcome, string? TransactionId, string Reason);

public interface IPaymentClient
{
    Task<PaymentResult> ProcessPaymentAsync(
        Guid reservationId,
        string userId,
        decimal amount,
        string correlationId,
        CancellationToken ct = default);
}
