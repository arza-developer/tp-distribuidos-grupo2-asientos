using Availability.Domain.Entities;

namespace Availability.Infrastructure.Persistence;

/// <summary>Datos iniciales deterministas (mismos asientos que el mock de Availability.Api).</summary>
public static class SeedData
{
    public static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IReadOnlyList<Seat> Seats() =>
    [
        NewSeat("aaaaaaaa-0000-0000-0000-000000000001", "A-1", 15000m, true),
        NewSeat("aaaaaaaa-0000-0000-0000-000000000002", "A-2", 15000m, true),
        NewSeat("aaaaaaaa-0000-0000-0000-000000000003", "A-3", 18000m, false),
        NewSeat("bbbbbbbb-0000-0000-0000-000000000001", "B-1", 12000m, true),
    ];

    private static Seat NewSeat(string seatId, string number, decimal price, bool available) => new()
    {
        SeatId = Guid.Parse(seatId), EventId = EventId, SeatNumber = number, Price = price, IsAvailable = available
    };
}
