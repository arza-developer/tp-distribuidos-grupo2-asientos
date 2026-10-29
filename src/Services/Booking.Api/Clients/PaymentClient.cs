using Grpc.Core;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SeatsReservation.Contracts.Grpc;

namespace Booking.Api.Clients;

/// <summary>
/// Cliente gRPC hacia Payment.
/// </summary>
public class PaymentClient : IPaymentClient
{
    public const string FailedReason = "pago fallido";
    public const string UnavailableReason = "pago no disponible";

    private readonly PaymentApi.PaymentApiClient _client;
    private readonly ILogger<PaymentClient> _logger;

    public PaymentClient(PaymentApi.PaymentApiClient client, ILogger<PaymentClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        Guid reservationId,
        string userId,
        decimal amount,
        string correlationId,
        CancellationToken ct = default)
    {
        var request = new PaymentRequest
        {
            ReservationId = reservationId.ToString(),
            UserId = userId,
            Amount = (double)amount,
            CorrelationId = correlationId
        };

        _logger.LogInformation(
            "[PAYMENT CLIENT] Enviando cobro de reserva {ReservationId}, monto {Amount}, correlación {CorrelationId}",
            reservationId, amount, correlationId);

        try
        {
            var response = await _client.ProcessPaymentAsync(request, cancellationToken: ct);

            if (response.IsSuccess)
            {
                _logger.LogInformation(
                    "[PAYMENT CLIENT] Cobro confirmado para reserva {ReservationId}, transacción {TransactionId}",
                    reservationId, response.TransactionId);
                return new PaymentResult(PaymentOutcome.Confirmed, response.TransactionId, string.Empty);
            }

            _logger.LogWarning(
                "[PAYMENT CLIENT] Cobro rechazado para reserva {ReservationId}: {RejectionReason}",
                reservationId, response.RejectionReason);
            return new PaymentResult(PaymentOutcome.Failed, null, FailedReason);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && IsUnavailable(ex))
        {
            _logger.LogError(ex,
                "[PAYMENT CLIENT] Payment no disponible para reserva {ReservationId} (correlación {CorrelationId})",
                reservationId, correlationId);
            return new PaymentResult(PaymentOutcome.Unavailable, null, UnavailableReason);
        }
    }

    private static bool IsUnavailable(Exception ex) =>
        ex is RpcException
            or BrokenCircuitException
            or TimeoutRejectedException
            or HttpRequestException;
}