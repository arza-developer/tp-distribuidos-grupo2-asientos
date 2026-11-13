using System.Globalization;
using Availability.Application.Reservations.Commands.SyncReservationStatus;
using Availability.Domain.Enums;

namespace Availability.Web.Mvc.Models;

/// <summary>
/// Datos del formulario de reserva. El precio llega como TEXTO y se interpreta con cultura invariante: el model binder usa la
/// cultura del servidor y podría leer "15000.00" como 1.500.000. El precio es solo informativo (el handler usa el del
/// catálogo). NO hay <c>userId</c>: el usuario sale de la sesión.
/// </summary>
public sealed class ReserveForm
{
    public Guid EventId { get; set; }

    public string? SeatNumber { get; set; }

    public string? Price { get; set; }

    public decimal ParsedPrice =>
        decimal.TryParse(Price, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) && p >= 0 ? p : 0m;
}

public sealed class ReservationStatusViewModel
{
    public required ReservationStatusResult Result { get; init; }
    public string Message => Result.Status switch
    {
        ReservationStatus.Pending => "Su reserva aún no fue procesada",
        ReservationStatus.Retained => "Se ha generado su reserva, esta pendiente de confirmación de pago",
        ReservationStatus.Confirmed => "Reserva confirmada!",
        ReservationStatus.Rejected => "Su reserva se ha rechazado",
        ReservationStatus.Expired => "Su reserva a expirado por no confirmarse un pago",
        _ => ""
    };
    public string StatusLabel => Result.Status switch
    {
        ReservationStatus.Pending => "Pendiente de procesamiento",
        ReservationStatus.Retained => "Retenida",
        ReservationStatus.Confirmed => "Confirmada",
        ReservationStatus.Rejected => "Rechazada",
        ReservationStatus.Expired => "Vencida",
        _ => "Desconocido"
    };

    /// <summary>Clase CSS del badge: verde (activa/confirmada), ámbar (en curso), roja (rechazada/vencida).</summary>
    public string BadgeClass => Result.Status switch
    {
        ReservationStatus.Confirmed => "ok",
        ReservationStatus.Pending or ReservationStatus.Retained => "warn",
        _ => "bad"
    };

    public string? ExpiresText => Result.ExpiresAtUtc is { } e
        ? e.ToUniversalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " UTC"
        : null;
}

/// <summary>Página de error amigable: nunca lleva detalles internos, pero sí el código de seguimiento (CorrelationId).</summary>
public sealed record ProblemViewModel(string Title, string Message, int StatusCode, string CorrelationId);
