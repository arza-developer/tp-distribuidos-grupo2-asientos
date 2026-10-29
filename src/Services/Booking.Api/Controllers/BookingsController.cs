using Booking.Api.Clients;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Controllers;

public record PayBookingRequest(string UserId, decimal Amount);

public record PayBookingResponse(Guid ReservationId, string Status, string? Reason, string? TransactionId);

/// <summary>
/// [Patrón Arquitectónico: N-Layer - Capa de Presentación / API]
/// [Requisito 3.b]: API REST interna para consulta de estados de reservas.
/// </summary>
[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly ILogger<BookingsController> _logger;
    private readonly IPaymentClient _paymentClient;

    public BookingsController(ILogger<BookingsController> logger, IPaymentClient paymentClient)
    {
        _logger = logger;
        _paymentClient = paymentClient;
    }

    /// <summary>
    /// [Pasos 5 y 6 Consigna]: Cobra una reserva retenida invocando a Payment por gRPC y mapea el resultado
    /// a un estado de negocio: Confirmada, Rechazada (pago fallido) o Rechazada (pago no disponible).
    /// </summary>
    [HttpPost("{id:guid}/pay")]
    public async Task<ActionResult<PayBookingResponse>> PayBooking(
        Guid id,
        [FromBody] PayBookingRequest body,
        CancellationToken ct)
    {
        var correlationId = Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();

        var result = await _paymentClient.ProcessPaymentAsync(id, body.UserId, body.Amount, correlationId, ct);

        var response = result.Outcome switch
        {
            PaymentOutcome.Confirmed => new PayBookingResponse(id, "Confirmada", null, result.TransactionId),
            _ => new PayBookingResponse(id, "Rechazada", result.Reason, null)
        };

        _logger.LogInformation("[BOOKING API] Reserva {ReservationId} => {Status} {Reason} (correlación {CorrelationId})",
            id, response.Status, response.Reason, correlationId);

        return Ok(response);
    }

    /// <summary>
    /// [Paso 7 Consigna]: Endpoint consultado internamente por Availability vía REST GET.
    /// </summary>
    /// <param name="id">Identificador único de la reserva.</param>
    /// <returns>Estado actual del ciclo de vida de la reserva.</returns>
    [HttpGet("{id:guid}")]
    public IActionResult GetBookingStatus(Guid id)
    {
        _logger.LogInformation("[BOOKING API] Consulta de estado para reserva {ReservationId}", id);

        // [Máquina de Estados de la Consigna]: Retenida, Confirmada, Rechazada, Vencida
        return Ok(new
        {
            ReservationId = id,
            SeatNumber = "A-1",
            Status = "Retenida",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        });
    }
}
