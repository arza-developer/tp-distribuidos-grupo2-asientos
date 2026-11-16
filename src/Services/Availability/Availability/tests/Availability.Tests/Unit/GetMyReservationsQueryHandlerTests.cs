using Availability.Application.Reservations.Queries.GetMyReservations;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Tests.Support;

namespace Availability.Tests.Unit;

public class GetMyReservationsQueryHandlerTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid EventA = Guid.NewGuid();

    private static SeatRequest Req(Guid user, string seat, ReservationStatus status, DateTime at) => new()
    {
        ReservationId = Guid.NewGuid(), SeatId = Guid.NewGuid(), EventId = EventA, SeatNumber = seat,
        UserId = user, Price = 1m, Status = status, RequestedAtUtc = at, CorrelationId = "c"
    };

    private static GetMyReservationsQueryHandler Create(Guid? user, params SeatRequest[] items)
    {
        var repo = new InMemorySeatRequestRepository();
        repo.Items.AddRange(items);
        return new GetMyReservationsQueryHandler(repo, new FakeCurrentUserAccessor(user));
    }

    [Fact]
    public async Task ReturnsOnlyTheReservationsOfTheAuthenticatedUser()
    {
        var mine = Req(Me, "A-1", ReservationStatus.Retained, DateTime.UtcNow);
        var handler = Create(Me, mine, Req(Other, "A-2", ReservationStatus.Retained, DateTime.UtcNow));

        var result = await handler.Handle(new GetMyReservationsQuery(), default);

        var item = Assert.Single(result);
        Assert.Equal((mine.ReservationId, EventA, "A-1", ReservationStatus.Retained),
            (item.ReservationId, item.EventId, item.SeatNumber, item.Status));
    }

    [Fact]
    public async Task WhenThereAreSeveralForTheSameSeat_TheMostRecentOneWins()
    {
        var old = Req(Me, "A-1", ReservationStatus.Rejected, DateTime.UtcNow.AddHours(-2));
        var latest = Req(Me, "A-1", ReservationStatus.Pending, DateTime.UtcNow);
        var handler = Create(Me, old, latest);

        var result = await handler.Handle(new GetMyReservationsQuery(), default);

        Assert.Equal(latest.ReservationId, Assert.Single(result).ReservationId);
    }

    [Fact]
    public async Task WithoutAUser_ReturnsNothing()
    {
        var handler = Create(user: null, Req(Me, "A-1", ReservationStatus.Retained, DateTime.UtcNow));

        Assert.Empty(await handler.Handle(new GetMyReservationsQuery(), default));
    }
}
