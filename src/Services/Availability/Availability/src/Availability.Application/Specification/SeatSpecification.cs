using System.Linq.Expressions;
using Availability.Domain.Entities;

namespace Availability.Application.Specification;

/// <summary>
/// Consultas reutilizables sobre <see cref="Seat"/>. Cada miembro devuelve el predicado (<c>Expression</c>, que EF traduce
/// a SQL) con un nombre de intención; se pasan a <c>ListAsync</c>/<c>FirstOrDefaultAsync</c> de los repositorios.
/// Los parámetros viajan como parámetros de la consulta (no se concatenan valores).
/// </summary>
public static class SeatSpecification
{
    /// <summary>El asiento de ese evento con ese número exacto (comparación ordinal, sensible a mayúsculas).</summary>
    public static Expression<Func<Seat, bool>> ByEventAndNumber(Guid eventId, string seatNumber) =>
        s => s.EventId == eventId && s.SeatNumber == seatNumber;

    /// <summary>El asiento con esa referencia externa (<c>SeatId</c>, nunca el Id interno).</summary>
    public static Expression<Func<Seat, bool>> BySeatId(Guid seatId) =>
        s => s.SeatId == seatId;

    /// <summary>Los asientos de un evento; con <paramref name="eventId"/> nulo, los de todos los eventos.</summary>
    public static Expression<Func<Seat, bool>> ForEvent(Guid? eventId) =>
        s => eventId == null || s.EventId == eventId;
}
