using Grpc.Core;
using Microsoft.Extensions.Logging;
using SeatsReservation.Contracts.Grpc;

namespace Payment.Grpc.Services;

public class PaymentService : PaymentApi.PaymentApiBase
{
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ILogger<PaymentService> logger)
    {
        _logger = logger;
    }

    public override Task<PaymentResponse> ProcessPayment(PaymentRequest request, ServerCallContext context)
    {
        _logger.LogInformation("[PAYMENT GRPC] Procesando cobro para reserva {ReservationId}, monto {Amount}", 
            request.ReservationId, request.Amount);

        return Task.FromResult(new PaymentResponse
        {
            IsSuccess = true,
            TransactionId = Guid.NewGuid().ToString(),
            RejectionReason = string.Empty
        });
    }
}
