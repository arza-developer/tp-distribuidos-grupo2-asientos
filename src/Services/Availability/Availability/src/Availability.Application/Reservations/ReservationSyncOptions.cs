namespace Availability.Application.Reservations;

/// <summary>Parámetros de la sincronización de estado con Booking (sección <c>ReservationSync</c>).</summary>
public class ReservationSyncOptions
{
    public const string SectionName = "ReservationSync";

    /// <summary>
    /// Minutos que una solicitud <c>Pending</c> puede seguir sin que Booking la conozca antes de considerarla
    /// huérfana (se marca <c>Expired</c> y se libera el asiento). Debe superar la retención de Booking y su demora
    /// normal para tomar el mensaje de la cola.
    /// </summary>
    public int PendingTimeoutMinutes { get; set; } = 10;
}
