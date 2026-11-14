using Availability.Application.Reservations.Commands.RequestReservation;
using Availability.Application.Reservations.Commands.SyncReservationStatus;
using Availability.Domain.Enums;
using Availability.Web.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = "Client")]
public class ReservationsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Solicita la reserva de un asiento. Responde 202 sin esperar a Booking (consistencia eventual).
    /// Requiere token Bearer con rol Client (Auth Server).
    /// </summary>
    [HttpPost("reservations")]
    [ProducesResponseType(typeof(ReservationAcceptedDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] ReservationRequestDto request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RequestReservationCommand(request.EventId, request.SeatNumber, request.UserId, request.Price), ct);

        var checkStatusUrl = $"/api/reservations/{result.ReservationId}";

        // Location apunta al GET de estado (mismo valor que checkStatusUrl).
        return Accepted(checkStatusUrl, new ReservationAcceptedDto(
            result.ReservationId,
            ReservationStatus.Pending.ToString(),
            "Solicitud recibida y encolada para procesamiento",
            checkStatusUrl));
    }

    /// <summary>
    /// Paso 7 del TP: estado de la reserva, consultado a Booking a través de Availability. Además sincroniza el
    /// estado local (y libera el asiento si Booking rechazó/venció la reserva): ver SyncReservationStatusCommandHandler.
    /// Requiere token Bearer con rol Client (Auth Server).
    /// </summary>
    [HttpGet("reservations/{id:guid}")]
    [ProducesResponseType(typeof(ReservationStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ReservationStatusDto>> GetStatus(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new SyncReservationStatusCommand(id), ct);

        return Ok(new ReservationStatusDto(
            result.ReservationId,
            result.Status.ToString(),
            result.Message,
            result.SeatNumber,
            result.ExpiresAtUtc,
            result.RejectionReason));
    }
}
