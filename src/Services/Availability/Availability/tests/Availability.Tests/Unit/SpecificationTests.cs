using Availability.Application.Specification;
using Availability.Domain.Entities;
using Availability.Domain.Enums;

namespace Availability.Tests.Unit;

/// <summary>Semántica de cada especificación, evaluada en memoria (la traducción a SQL se prueba aparte, contra SQLite).</summary>
public class SpecificationTests
{
    private static readonly Guid EventA = Guid.NewGuid();
    private static readonly Guid EventB = Guid.NewGuid();

    private static Seat NewSeat(Guid eventId, string number) => new()
    {
        SeatId = Guid.NewGuid(), EventId = eventId, SeatNumber = number, Price = 1m, IsAvailable = true
    };

    private static SeatRequest NewRequest(Guid seatId, ReservationStatus status, Guid? reservationId = null) => new()
    {
        ReservationId = reservationId ?? Guid.NewGuid(), SeatId = seatId, EventId = EventA, SeatNumber = "A-1",
        UserId = Guid.NewGuid(), Price = 1m, Status = status, RequestedAtUtc = DateTime.UtcNow, CorrelationId = "c"
    };

    // ---------- SeatSpecification ----------

    [Fact]
    public void ByEventAndNumber_MatchesOnlyThatEventAndExactNumber()
    {
        var spec = SeatSpecification.ByEventAndNumber(EventA, "A-1").Compile();

        Assert.True(spec(NewSeat(EventA, "A-1")));
        Assert.False(spec(NewSeat(EventB, "A-1"))); // otro evento
        Assert.False(spec(NewSeat(EventA, "A-2"))); // otro número
        Assert.False(spec(NewSeat(EventA, "a-1"))); // sensible a mayúsculas
        Assert.False(spec(NewSeat(EventA, "A-1 "))); // sin recorte de espacios: el handler recorta antes
    }

    [Fact]
    public void BySeatId_MatchesTheExternalReference_NotTheInternalId()
    {
        var seat = NewSeat(EventA, "A-1");
        seat.Id = 99;
        var spec = SeatSpecification.BySeatId(seat.SeatId).Compile();

        Assert.True(spec(seat));
        Assert.False(spec(NewSeat(EventA, "A-1")));
        Assert.False(SeatSpecification.BySeatId(Guid.Empty).Compile()(seat));
    }

    [Fact]
    public void ForEvent_WithAnEvent_FiltersToIt()
    {
        var spec = SeatSpecification.ForEvent(EventA).Compile();

        Assert.True(spec(NewSeat(EventA, "A-1")));
        Assert.False(spec(NewSeat(EventB, "B-1")));
    }

    [Fact]
    public void ForEvent_WithNull_MatchesEveryEvent()
    {
        var spec = SeatSpecification.ForEvent(null).Compile();

        Assert.True(spec(NewSeat(EventA, "A-1")));
        Assert.True(spec(NewSeat(EventB, "B-1")));
    }

    // ---------- SeatRequestSpecification ----------

    [Fact]
    public void ByReservationId_MatchesTheExternalReference_NotTheInternalId()
    {
        var request = NewRequest(Guid.NewGuid(), ReservationStatus.Pending);
        request.Id = 7;
        var spec = SeatRequestSpecification.ByReservationId(request.ReservationId).Compile();

        Assert.True(spec(request));
        Assert.False(spec(NewRequest(Guid.NewGuid(), ReservationStatus.Pending)));
    }

    [Fact]
    public void ActiveStatuses_AreThoseThatHoldTheSeat()
    {
        Assert.Equal(
            [ReservationStatus.Pending, ReservationStatus.Retained, ReservationStatus.Confirmed],
            SeatRequestSpecification.ActiveStatuses.Order());
        Assert.DoesNotContain(ReservationStatus.Rejected, SeatRequestSpecification.ActiveStatuses);
        Assert.DoesNotContain(ReservationStatus.Expired, SeatRequestSpecification.ActiveStatuses);
    }

    [Theory]
    [InlineData(ReservationStatus.Pending, true)]
    [InlineData(ReservationStatus.Retained, true)]
    [InlineData(ReservationStatus.Confirmed, true)]
    [InlineData(ReservationStatus.Rejected, false)]
    [InlineData(ReservationStatus.Expired, false)]
    public void ActiveForSeat_OnlyMatchesRequestsThatStillHoldTheSeat(ReservationStatus status, bool expected)
    {
        var seatId = Guid.NewGuid();
        var spec = SeatRequestSpecification.ActiveForSeat(seatId, excludingReservationId: Guid.NewGuid()).Compile();

        Assert.Equal(expected, spec(NewRequest(seatId, status)));
    }

    [Fact]
    public void ActiveForSeat_ExcludesTheGivenReservation()
    {
        var seatId = Guid.NewGuid();
        var mine = NewRequest(seatId, ReservationStatus.Retained);
        var other = NewRequest(seatId, ReservationStatus.Retained);
        var spec = SeatRequestSpecification.ActiveForSeat(seatId, mine.ReservationId).Compile();

        Assert.False(spec(mine)); // la propia solicitud no cuenta
        Assert.True(spec(other));
    }

    [Fact]
    public void ActiveForSeat_IgnoresRequestsOfOtherSeats()
    {
        var spec = SeatRequestSpecification.ActiveForSeat(Guid.NewGuid(), Guid.NewGuid()).Compile();

        Assert.False(spec(NewRequest(Guid.NewGuid(), ReservationStatus.Confirmed)));
    }
}
