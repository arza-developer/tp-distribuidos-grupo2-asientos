using Microsoft.AspNetCore.Mvc;
using SeatsReservation.Contracts.Events;

namespace Availability.Api.Controllers;

/// <summary>
/// [Patrón Arquitectónico: Clean Architecture - Adaptador de Entrada / Controlador]
/// [Requisito 3.a]: Desacopla la interfaz HTTP del modelo de dominio anémico.
/// Administra el catálogo de asientos y la recepción asíncrona de solicitudes de reserva.
/// </summary>
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

    /// <summary>
    /// [Paso 1 Consigna]: Consulta pública de asientos disponibles sin autenticación.
    /// </summary>
    /// <returns>Listado de asientos con estado y precio.</returns>
    [HttpGet("seats")]
    public IActionResult GetAvailableSeats()
    {
        // [Dominio Anémico]: Estructura pura de datos sin métodos de negocio
        var seats = new[]
        {
            new { SeatNumber = "A-1", Price = 15000.0, IsAvailable = true },
            new { SeatNumber = "A-2", Price = 15000.0, IsAvailable = true },
            new { SeatNumber = "A-3", Price = 18000.0, IsAvailable = false },
            new { SeatNumber = "B-1", Price = 12000.0, IsAvailable = true }
        };
        return Ok(seats);
    }

    /// <summary>
    /// [Paso 3 Consigna]: Recepción asíncrona de reserva con consistencia eventual.
    /// [Decisión de Arquitectura]: Responde 202 Accepted inmediatamente sin esperar a Booking.
    /// [Requisito 3.f]: Inicia la cadena de eventos con CorrelationId para auditoría.
    /// </summary>
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

        // [Patrón]: Publicación al Broker (Outbox/Azurite) para procesamiento desacoplado
        await _publisher.PublishAsync(message, "booking-requests-queue");

        // [Requisito 3.f]: Registro explícito de INICIO de la cadena de sucesos
        _logger.LogInformation("[{CorrelationId}] INICIO: Solicitud de reserva recibida para asiento {SeatNumber}. ID asignado: {ReservationId}",
            correlationId, dto.SeatNumber, reservationId);

        return Accepted(new
        {
            ReservationId = reservationId,
            Status = "Pending",
            Message = "Solicitud recibida y encolada para procesamiento",
            CheckStatusUrl = $"/api/reservations/{reservationId}"
        });
    }

    /// <summary>
    /// [Paso 7 Consigna]: Polling de estado de reserva consultado por el usuario.
    /// </summary>
    [HttpGet("reservations/{id:guid}")]
    public IActionResult GetReservationStatus(Guid id)
    {
        return Ok(new
        {
            ReservationId = id,
            Status = "Pending",
            Message = "La reserva está siendo procesada por Booking"
        });
    }
}

/// <summary>
/// [Dominio Anémico - Requisito 3.a.ii]: DTO inmutable de transporte de datos.
/// </summary>
public record ReservationRequestDto(Guid EventId, string SeatNumber, Guid UserId, decimal Price);
