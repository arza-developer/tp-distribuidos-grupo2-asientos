using Availability.Application.Abstractions;
using Availability.Application.Exceptions;
using Availability.Application.Reservations.Commands.RequestReservation;
using Availability.Application.Reservations.Commands.SyncReservationStatus;
using Availability.Domain.Exceptions;
using Availability.Web.Mvc.Models;
using Availability.Web.Mvc.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Mvc.Controllers;

/// <summary>
/// Reservar y ver el estado desde las páginas. Mismos casos de uso que la API (MediatR en proceso). La sesión es la
/// cookie, elegida a nivel general: el controller solo exige el rol.
/// <para>
/// El <c>userId</c> sale de la sesión (nunca del formulario) y los errores se muestran en la página, sin pasar por el JSON
/// de <c>ApiExceptionHandler</c>: de negocio → mensaje en el catálogo; técnicos → página 503 con el código de seguimiento.
/// </para>
/// </summary>
[Authorize(Roles = SessionLoginService.RequiredRole)]
public class ReservationController(
    IMediator mediator,
    ICurrentUserAccessor currentUser,
    ICorrelationIdAccessor correlation) : MvcControllerBase
{
    /// <summary>Solicita la reserva (202 en la API; aquí PRG: redirige al estado). Nunca se llama por GET.</summary>
    [HttpPost]
    public async Task<IActionResult> Reserve(ReserveForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(form.SeatNumber) || currentUser.UserId is not { } userId)
            return BackToCatalog("Los datos de la reserva no son válidos.");

        try
        {
            var result = await mediator.Send(
                new RequestReservationCommand(form.EventId, form.SeatNumber, userId, form.ParsedPrice), ct);

            TempData["Info"] = "Solicitud recibida: Booking la está procesando. Actualizá esta página para ver el resultado.";
            return RedirectToAction(nameof(Status), new { id = result.ReservationId });
        }
        catch (SeatUnavailableException)
        {
            return BackToCatalog($"El asiento {form.SeatNumber.Trim()} ya no está disponible.");
        }
        catch (SeatNotFoundException)
        {
            return BackToCatalog("El asiento elegido no existe.");
        }
        catch (BusinessException)
        {
            return BackToCatalog("No se pudo registrar la reserva con los datos enviados.");
        }
        catch (MessagePublishException)
        {
            return ProblemPage(StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                "No se pudo registrar la solicitud en este momento. El asiento sigue disponible: intentá nuevamente en unos minutos.");
        }
    }

    /// <summary>
    /// Estado de la reserva (paso 7 del TP). Consulta a Booking y sincroniza el estado local (libera el asiento si Booking la
    /// rechazó o venció): ver <c>SyncReservationStatusCommandHandler</c>. El refresco es manual (enlace Actualizar).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new SyncReservationStatusCommand(id), ct);
            return View(new ReservationStatusViewModel { Result = result });
        }
        catch (ReservationNotFoundException)
        {
            // También cubre las reservas de otros usuarios: no se revela su existencia.
            return ProblemPage(StatusCodes.Status404NotFound, "No existe la reserva",
                "No encontramos esa reserva en tu cuenta.");
        }
        catch (BookingUnavailableException)
        {
            return ProblemPage(StatusCodes.Status503ServiceUnavailable, "No pudimos consultar el estado",
                "El servicio de reservas no responde en este momento. Tu reserva no se perdió: actualizá la página en unos minutos.");
        }
    }

    private IActionResult BackToCatalog(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction("Index", "Home");
    }

    private IActionResult ProblemPage(int statusCode, string title, string message)
    {
        Response.StatusCode = statusCode;
        return View("~/Views/Shared/Problem.cshtml", new ProblemViewModel(title, message, statusCode, correlation.CorrelationId));
    }
}
