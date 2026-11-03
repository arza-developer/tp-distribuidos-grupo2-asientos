using Availability.Application.Reservations.Queries.GetMyReservations;
using Availability.Application.Seats.Queries.GetSeatCatalog;
using Availability.Web.Mvc.Models;
using Availability.Web.Mvc.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Mvc.Controllers;

/// <summary>Catálogo público de asientos (paso 1 del TP: consulta sin login). Reservar requiere sesión.</summary>
public class HomeController(IMediator mediator) : MvcControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(Guid? eventId, CancellationToken ct)
    {
        // Un eventId mal formado no rompe la página: se ignora el filtro y se avisa en pantalla.
        var invalidFilter = !ModelState.IsValid;
        var filter = invalidFilter ? null : eventId;

        var catalog = await mediator.Send(new GetSeatCatalogQuery(filter), ct);
        var canReserve = User.IsInRole(SessionLoginService.RequiredRole);
        // Solo con sesión: para cada asiento donde el usuario tiene una reserva se ofrece el enlace a su estado.
        var mine = canReserve ? await mediator.Send(new GetMyReservationsQuery(), ct) : [];

        return View(new CatalogViewModel
        {
            Events = catalog
                .GroupBy(s => s.EventId)
                .Select(g => new EventSeatsGroup(g.Key, g.ToList()))
                .OrderBy(g => g.EventId)
                .ToList(),
            FilteredEventId = filter,
            InvalidEventFilter = invalidFilter,
            CanReserve = canReserve,
            MyReservations = mine
        });
    }
}
