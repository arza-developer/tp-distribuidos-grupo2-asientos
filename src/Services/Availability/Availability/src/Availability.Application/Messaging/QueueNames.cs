namespace Availability.Application.Messaging;

/// <summary>Nombres de colas del broker (contrato con Booking).</summary>
public static class QueueNames
{
    /// <summary>Solicitudes de reserva que Availability publica y Booking consume.</summary>
    public const string BookingRequests = "booking-requests-queue";
}
