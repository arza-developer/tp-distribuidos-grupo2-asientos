using System.Globalization;
using Availability.Application.Reservations.Queries.GetMyReservations;
using Availability.Application.Seats.Queries.GetSeatCatalog;

namespace Availability.Web.Mvc.Models;

/// <summary>Asientos de un evento (no existe la entidad Event: se agrupa por su identificador, limitación M5).</summary>
public sealed record EventSeatsGroup(Guid EventId, IReadOnlyList<SeatCatalogItem> Seats);

public sealed class CatalogViewModel
{
    public required IReadOnlyList<EventSeatsGroup> Events { get; init; }

    /// <summary>Evento por el que se filtró (<c>?eventId=</c>), si hubo.</summary>
    public Guid? FilteredEventId { get; init; }

    /// <summary>El <c>eventId</c> recibido no era un Guid válido: se muestran todos los eventos.</summary>
    public bool InvalidEventFilter { get; init; }

    /// <summary>Solo un usuario con sesión y rol Client ve los botones Reservar.</summary>
    public bool CanReserve { get; init; }

    /// <summary>Última reserva del usuario por asiento (vacío si no hay sesión): permite enlazar el asiento con su estado.</summary>
    public IReadOnlyList<MyReservationItem> MyReservations { get; init; } = [];

    /// <summary>La reserva del usuario para ese asiento, si existe.</summary>
    public MyReservationItem? ReservationFor(SeatCatalogItem seat) =>
        MyReservations.FirstOrDefault(r => r.EventId == seat.EventId && r.SeatNumber == seat.SeatNumber);

    public int AvailableCount => Events.Sum(e => e.Seats.Count(s => s.IsAvailable));
}

/// <summary>Formato de precio independiente de la cultura del servidor (en contenedores puede ser invariante).</summary>
public static class PriceFormat
{
    private static readonly NumberFormatInfo Display = new() { NumberGroupSeparator = ".", NumberDecimalSeparator = ",", NumberDecimalDigits = 0 };

    /// <summary>"$ 15.000": para mostrar.</summary>
    public static string ForDisplay(decimal price) => "$ " + price.ToString("N0", Display);

    /// <summary>Valor invariante para el campo oculto del formulario (se envía y se enlaza con cultura invariante).</summary>
    public static string ForForm(decimal price) => price.ToString(CultureInfo.InvariantCulture);
}
