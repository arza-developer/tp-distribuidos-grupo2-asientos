using Availability.Application.Reservations.Commands.RequestReservation;
using Availability.Domain.Enums;
using Availability.Web.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Controllers;

[ApiController]
[Route("api")]
public class ReservationsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Solicita la reserva de un asiento. Responde 202 sin esperar a Booking (consistencia eventual).
    /// Pendiente: requerirá JWT en T4.1.
    /// </summary>
    [HttpPost("reservations")]
    [ProducesResponseType(typeof(ReservationAcceptedDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] ReservationRequestDto request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RequestReservationCommand(request.EventId, request.SeatNumber, request.UserId, request.Price), ct);

        return Accepted(new ReservationAcceptedDto(
            result.ReservationId,
            ReservationStatus.Pending.ToString(),
            "Solicitud recibida y encolada para procesamiento",
            $"/api/reservations/{result.ReservationId}"));
    }
}
