using Availability.Application.Seats.Queries.GetSeats;
using Availability.Domain.Entities;

namespace Availability.Tests.Unit;

public class GetSeatsQueryHandlerTests
{
    private static readonly Guid EventA = Guid.NewGuid();
    private static readonly Guid EventB = Guid.NewGuid();

    private static Seat NewSeat(Guid eventId, string number, decimal price, bool available) => new()
    {
        SeatId = Guid.NewGuid(), EventId = eventId, SeatNumber = number, Price = price, IsAvailable = available
    };

    private static (GetSeatsQueryHandler Handler, InMemorySeatRepository Repo) Create(params Seat[] seats)
    {
        var repo = new InMemorySeatRepository();
        repo.Items.AddRange(seats);
        return (new GetSeatsQueryHandler(repo), repo);
    }

    [Fact]
    public async Task Handle_MapsSeatsToDto()
    {
        var (handler, _) = Create(NewSeat(EventA, "A-1", 15000m, true));

        var result = await handler.Handle(new GetSeatsQuery(), default);

        var dto = Assert.Single(result);
        Assert.Equal("A-1", dto.SeatNumber);
        Assert.Equal(15000m, dto.Price);
        Assert.True(dto.IsAvailable);
    }

    [Fact]
    public async Task Handle_OrdersBySeatNumber()
    {
        var (handler, _) = Create(
            NewSeat(EventA, "B-1", 1m, true), NewSeat(EventA, "A-2", 1m, true), NewSeat(EventA, "A-1", 1m, true));

        var result = await handler.Handle(new GetSeatsQuery(), default);

        Assert.Equal(["A-1", "A-2", "B-1"], result.Select(s => s.SeatNumber));
    }

    [Fact]
    public async Task Handle_FiltersByEventId()
    {
        var (handler, _) = Create(NewSeat(EventA, "A-1", 1m, true), NewSeat(EventB, "A-1", 1m, false));

        var result = await handler.Handle(new GetSeatsQuery(EventB), default);

        var dto = Assert.Single(result);
        Assert.False(dto.IsAvailable);
    }

    [Fact]
    public async Task Handle_WithoutEventId_ReturnsAllEvents()
    {
        var (handler, _) = Create(NewSeat(EventA, "A-1", 1m, true), NewSeat(EventB, "A-1", 1m, true));

        Assert.Equal(2, (await handler.Handle(new GetSeatsQuery(), default)).Count);
    }

    [Fact]
    public async Task Handle_UnknownEvent_ReturnsEmpty()
    {
        var (handler, _) = Create(NewSeat(EventA, "A-1", 1m, true));

        Assert.Empty(await handler.Handle(new GetSeatsQuery(Guid.NewGuid()), default));
    }
}
