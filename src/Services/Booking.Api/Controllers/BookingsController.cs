using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Controllers;

/// <summary>
/// [Patrón Arquitectónico: N-Layer - Capa de Presentación / API]
/// [Requisito 3.b]: API REST interna para consulta de estados de reservas.
/// </summary>
[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(ILogger<BookingsController> logger)
    {
        _logger = logger;
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
