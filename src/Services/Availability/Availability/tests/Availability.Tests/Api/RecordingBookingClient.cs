using Availability.Application.Abstractions;
using Availability.Application.Bookings;

namespace Availability.Tests.Api;

/// <summary>Fake de Booking para tests de API: respuesta configurable, puede fallar y registra las consultas.</summary>
public sealed class RecordingBookingClient : IBookingClient
{
    public BookingStatusInfo? Result { get; set; }
    public Exception? Throw { get; set; }
    public List<Guid> Calls { get; } = [];

    public Task<BookingStatusInfo?> GetStatusAsync(Guid reservationId, CancellationToken ct = default)
    {
        Calls.Add(reservationId);
        if (Throw is not null) throw Throw;
        return Task.FromResult(Result);
    }

    public void Reset()
    {
        Result = null;
        Throw = null;
        Calls.Clear();
    }
}
