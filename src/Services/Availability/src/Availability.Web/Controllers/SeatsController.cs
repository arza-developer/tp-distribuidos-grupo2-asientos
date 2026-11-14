using Availability.Application.Seats;
using Availability.Application.Seats.Queries.GetSeats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Controllers;

[ApiController]
[Route("api")]
public class SeatsController(IMediator mediator) : ControllerBase
{
    /// <summary>Catálogo público de asientos (sin autenticación). Filtro opcional por evento.</summary>
    [HttpGet("seats")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<SeatDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SeatDto>>> GetSeats([FromQuery] Guid? eventId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSeatsQuery(eventId), ct));
}
