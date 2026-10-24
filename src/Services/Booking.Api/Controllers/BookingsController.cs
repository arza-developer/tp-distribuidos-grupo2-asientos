using Microsoft.AspNetCore.Mvc;

namespace Booking.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(ILogger<BookingsController> logger)
    {
        _logger = logger;
    }

    // Paso 7: Availability consulta a Booking el estado de una reserva
    [HttpGet("{id:guid}")]
    public IActionResult GetBookingStatus(Guid id)
    {
        _logger.LogInformation("[BOOKING API] Consulta de estado para reserva {ReservationId}", id);

        return Ok(new
        {
            ReservationId = id,
            SeatNumber = "A-1",
            Status = "Retenida", // Estados posibles: Retenida, Confirmada, Rechazada, Vencida
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        });
    }
}
