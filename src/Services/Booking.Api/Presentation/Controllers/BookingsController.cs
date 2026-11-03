using Booking.Api.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Booking.Api.Services;
using Booking.Api.Models.Enums;

namespace Booking.Api.Presentation.Controllers;

/// <summary>
/// [Patrón Arquitectónico: N-Layer - Capa de Presentación / API]
/// [Requisito 3.b]: API REST interna para consulta de estados de reservas.
/// </summary>
[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly ILogger<BookingsController> _logger;
    private readonly IBookingService _bookingService;

    public BookingsController(ILogger<BookingsController> logger, IPaymentClient paymentClient, IBookingService bookingService)
    {
        _logger = logger;
        _bookingService = bookingService;
    }

    /// <summary>
    /// [Paso 7 Consigna]: Endpoint consultado internamente por Availability vía REST GET.
    /// </summary>
    /// <param name="id">Identificador único de la reserva.</param>
    /// <returns>Estado actual del ciclo de vida de la reserva.</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBookingStatus(Guid id)
    {
        _logger.LogInformation("[BOOKING API] Consulta de estado para reserva {ReservationId}", id);

        //Consultamos la reserva en la capa de servicio para obtener su estado actual
        var reservation = await _bookingService.GetBookingStatusAsync(id);

        if (reservation == null)
        {
            return NotFound(new {ReservationId = id, Message = "No se encontró la reserva."});
        }

        //Traducimos los estados internos de la reserva a un formato legible para la API
        var status = reservation.Status switch
        {
            ReservationStatus.Retained => "Retained",
            ReservationStatus.Confirmed => "Confirmed",
            ReservationStatus.Rejected => "Rejected",
            ReservationStatus.Expired => "Expired",
            _ => "Desconocida"
        };
        
        return Ok(new
        {
            ReservationId = reservation.RequestId,
            SeatNumber = reservation.SeatNumber,
            Status = status,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            RejectionReason = reservation.RejectionReason?.ToString()
        });
    }
}
