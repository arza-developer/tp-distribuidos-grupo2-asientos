namespace Availability.Application.Messaging;

/// <summary>Nombres del broker (contrato con Booking).</summary>
public static class MessagingNames
{
    /// <summary>Topic de Azure Service Bus donde Availability publica las solicitudes de reserva y Booking (suscripción "booking") las consume.</summary>
    public const string BookingRequestsTopic = "grupo2-booking-topic";
}
