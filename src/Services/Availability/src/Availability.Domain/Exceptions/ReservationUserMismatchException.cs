namespace Availability.Domain.Exceptions;

/// <summary>
/// El <c>userId</c> de la solicitud no coincide con el usuario autenticado (<c>sub</c> del token): no se le cree al body
/// quién reserva (el Auth Server emite <c>sub = UserId</c>).
/// </summary>
public sealed class ReservationUserMismatchException()
    : BusinessForbiddenException("reservation.user_mismatch",
        "El userId de la solicitud no coincide con el usuario autenticado.");
