using System.Linq.Expressions;
using Availability.Domain.Entities;
using Availability.Domain.Enums;

namespace Availability.Application.Specification;

/// <summary>Consultas reutilizables sobre <see cref="SeatRequest"/> (ver <see cref="SeatSpecification"/>).</summary>
public static class SeatRequestSpecification
{
    /// <summary>
    /// Estados en los que una solicitud RETIENE el asiento (el guarda local <c>Seat.IsAvailable</c> debe seguir en false):
    /// pendiente de Booking, retenida o confirmada. Rechazada y vencida ya lo liberaron.
    /// </summary>
    public static readonly ReservationStatus[] ActiveStatuses =
        [ReservationStatus.Pending, ReservationStatus.Retained, ReservationStatus.Confirmed];

    /// <summary>La solicitud con esa referencia externa (<c>ReservationId</c>, nunca el Id interno).</summary>
    public static Expression<Func<SeatRequest, bool>> ByReservationId(Guid reservationId) =>
        r => r.ReservationId == reservationId;

    /// <summary>
    /// Otra solicitud ACTIVA sobre el mismo asiento (distinta de <paramref name="excludingReservationId"/>):
    /// si existe, el asiento no puede liberarse.
    /// </summary>
    public static Expression<Func<SeatRequest, bool>> ActiveForSeat(Guid seatId, Guid excludingReservationId) =>
        r => r.SeatId == seatId
             && r.ReservationId != excludingReservationId
             && ActiveStatuses.Contains(r.Status);
}
