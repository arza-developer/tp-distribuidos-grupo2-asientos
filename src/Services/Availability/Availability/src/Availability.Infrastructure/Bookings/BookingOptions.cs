namespace Availability.Infrastructure.Bookings;

public class BookingOptions
{
    public const string SectionName = "Booking";

    /// <summary>URL base de Booking. En contenedores: http://booking-api:5102.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5102";

    /// <summary>Timeout corto: si Booking no responde, la consulta falla rápido (503).</summary>
    public int TimeoutSeconds { get; set; } = 5;
}
