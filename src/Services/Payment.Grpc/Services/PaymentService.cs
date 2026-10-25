using Grpc.Core;
using Microsoft.Extensions.Logging;
using SeatsReservation.Contracts.Grpc;

namespace Payment.Grpc.Services;

/// <summary>
/// [Patrón Arquitectónico: N-Layer / gRPC Service]
/// [Requisito 3.c]: Servicio de pagos de alto rendimiento mediante HTTP/2 y Protobuf binario.
/// </summary>
public class PaymentService : PaymentApi.PaymentApiBase
{
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ILogger<PaymentService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// [Paso 5 Consigna]: Procesa el cobro de una reserva retenida invocada desde Booking.
    /// </summary>
    public override Task<PaymentResponse> ProcessPayment(PaymentRequest request, ServerCallContext context)
    {
        _logger.LogInformation("[PAYMENT GRPC] Procesando cobro para reserva {ReservationId}, monto {Amount}",
            request.ReservationId, request.Amount);

        // [Stub]: Simulación de cobro exitoso para flujo Happy Path
        return Task.FromResult(new PaymentResponse
        {
            IsSuccess = true,
            TransactionId = Guid.NewGuid().ToString(),
            RejectionReason = string.Empty
        });
    }
}
