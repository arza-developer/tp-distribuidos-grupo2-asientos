using Microsoft.AspNetCore.Mvc;
using SeatsReservation.Contracts.Events;

namespace Availability.Api.Controllers;

[ApiController]
[Route("api")]
public class ReservationsController : ControllerBase
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(IMessagePublisher publisher, ILogger<ReservationsController> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    // Paso 1 de la consigna: Consulta pública de asientos disponibles
    [HttpGet("seats")]
    public IActionResult GetAvailableSeats()
    {
        var seats = new[]
        {
            new { SeatNumber = "A-1", Price = 15000.0, IsAvailable = true },
            new { SeatNumber = "A-2", Price = 15000.0, IsAvailable = true },
            new { SeatNumber = "A-3", Price = 18000.0, IsAvailable = false },
            new { SeatNumber = "B-1", Price = 12000.0, IsAvailable = true }
        };
        return Ok(seats);
    }

    // Paso 3 de la consigna: Envío de solicitud de reserva (Asíncrono)
    [HttpPost("reservations")]
    public async Task<IActionResult> RequestReservation([FromBody] ReservationRequestDto dto)
    {
        var reservationId = Guid.NewGuid();
        var correlationId = HttpContext.TraceIdentifier;

        var message = new ReservationRequestedEvent
        {
            ReservationId = reservationId,
            EventId = dto.EventId,
            SeatNumber = dto.SeatNumber,
            UserId = dto.UserId,
            Price = dto.Price,
            RequestedAtUtc = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        // Publica en el broker (Mock/Azure) sin bloquear al cliente
        await _publisher.PublishAsync(message, "booking-requests-queue");

        _logger.LogInformation("[{CorrelationId}] INICIO: Solicitud de reserva recibida para asiento {SeatNumber}. Asignado ID {ReservationId}",
            correlationId, dto.SeatNumber, reservationId);

        // Responde de inmediato 202 Accepted
        return Accepted(new
        {
            ReservationId = reservationId,
            Status = "Pending",
            Message = "Solicitud recibida y encolada para procesamiento",
            CheckStatusUrl = $"/api/reservations/{reservationId}"
        });
    }

    // Paso 7 de la consigna: Polling del resultado
    [HttpGet("reservations/{id:guid}")]
    public IActionResult GetReservationStatus(Guid id)
    {
        // Stub inicial: simula respuesta de estado
        return Ok(new
        {
            ReservationId = id,
            Status = "Pending",
            Message = "La reserva está siendo procesada por Booking"
        });
    }
}

public record ReservationRequestDto(Guid EventId, string SeatNumber, Guid UserId, decimal Price);
