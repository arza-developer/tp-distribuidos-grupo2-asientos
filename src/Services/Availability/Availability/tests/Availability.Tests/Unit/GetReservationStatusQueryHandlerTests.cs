using Availability.Application.Abstractions;
using Availability.Application.Bookings;
using Availability.Application.Exceptions;
using Availability.Application.Reservations.Queries.GetReservationStatus;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Domain.Exceptions;

namespace Availability.Tests.Unit;

public class GetReservationStatusQueryHandlerTests
{
    private sealed class FakeBookingClient : IBookingClient
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
    }

    private readonly InMemorySeatRequestRepository _requests = new();
    private readonly FakeBookingClient _booking = new();
    private readonly GetReservationStatusQueryHandler _handler;
    private readonly SeatRequest _local;

    public GetReservationStatusQueryHandlerTests()
    {
        _local = new SeatRequest
        {
            ReservationId = Guid.NewGuid(), SeatId = Guid.NewGuid(), EventId = Guid.NewGuid(), SeatNumber = "A-1",
            UserId = Guid.NewGuid(), Price = 15000m, Status = ReservationStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow, CorrelationId = "corr"
        };
        _requests.Items.Add(_local);
        _handler = new GetReservationStatusQueryHandler(_requests, _booking);
    }

    private Task<ReservationStatusResult> Ask(Guid? id = null) =>
        _handler.Handle(new GetReservationStatusQuery(id ?? _local.ReservationId), default);

    [Fact]
    public async Task Handle_UnknownLocalReservation_ThrowsNotFound_WithoutCallingBooking()
    {
        var unknown = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ReservationNotFoundException>(() => Ask(unknown));

        Assert.Equal("reservation.not_found", ex.Code);
        Assert.Empty(_booking.Calls);
    }

    [Fact]
    public async Task Handle_BookingDoesNotKnowIt_ReturnsPending_EventualConsistency()
    {
        _booking.Result = null;

        var result = await Ask();

        Assert.Equal(ReservationStatus.Pending, result.Status);
        Assert.Equal("La reserva aún no fue procesada por Booking", result.Message);
        Assert.Equal(_local.ReservationId, result.ReservationId);
        Assert.Equal("A-1", result.SeatNumber);
        Assert.Null(result.ExpiresAtUtc);
        Assert.Equal([_local.ReservationId], _booking.Calls);
    }

    [Fact]
    public async Task Handle_Retained_ReturnsStatusMessageAndExpiry()
    {
        var expires = DateTime.UtcNow.AddMinutes(5);
        _booking.Result = new BookingStatusInfo(ReservationStatus.Retained, expires, "A-1");

        var result = await Ask();

        Assert.Equal(ReservationStatus.Retained, result.Status);
        Assert.Equal("Reserva retenida, pendiente de pago", result.Message);
        Assert.Equal(expires, result.ExpiresAtUtc);
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmed, "Reserva confirmada")]
    [InlineData(ReservationStatus.Rejected, "Reserva rechazada")]
    [InlineData(ReservationStatus.Expired, "La reserva venció")]
    public async Task Handle_FinalStatuses_ReturnMessage_AndNoExpiry(ReservationStatus status, string message)
    {
        _booking.Result = new BookingStatusInfo(status, DateTime.UtcNow.AddMinutes(5), "A-1");

        var result = await Ask();

        Assert.Equal(status, result.Status);
        Assert.Equal(message, result.Message);
        Assert.Null(result.ExpiresAtUtc); // el vencimiento solo se informa mientras está retenida
    }

    [Fact]
    public async Task Handle_UsesLocalSeatNumber_EvenIfBookingReportsOther()
    {
        _booking.Result = new BookingStatusInfo(ReservationStatus.Confirmed, null, "Z-9");

        Assert.Equal("A-1", (await Ask()).SeatNumber);
    }

    [Fact]
    public async Task Handle_BookingUnavailable_PropagatesTechnicalException_InsteadOfFakingPending()
    {
        _booking.Throw = new BookingUnavailableException("Booking caído");

        await Assert.ThrowsAsync<BookingUnavailableException>(() => Ask());
    }

    [Fact]
    public async Task Handle_IsPure_DoesNotModifyTheLocalRequest()
    {
        _booking.Result = new BookingStatusInfo(ReservationStatus.Rejected, null, "A-1");

        await Ask();

        Assert.Equal(ReservationStatus.Pending, _local.Status); // TD-1: la query no sincroniza el estado local
        Assert.Single(_requests.Items);
    }
}
