using Availability.Application.Abstractions;
using Availability.Application.Bookings;
using Availability.Application.Exceptions;
using Availability.Application.Reservations;
using Availability.Application.Reservations.Commands.SyncReservationStatus;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Domain.Exceptions;
using Availability.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Availability.Tests.Unit;

public class SyncReservationStatusCommandHandlerTests
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

    private readonly InMemorySeatRepository _seats = new();
    private readonly InMemorySeatRequestRepository _requests = new();
    private readonly FakeBookingClient _booking = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly SyncReservationStatusCommandHandler _handler;
    private readonly Seat _seat;
    private readonly SeatRequest _local;

    public SyncReservationStatusCommandHandlerTests()
    {
        // Estado tras un POST exitoso: asiento bloqueado y solicitud Pending.
        _seat = new Seat
        {
            SeatId = Guid.NewGuid(), EventId = Guid.NewGuid(), SeatNumber = "A-1", Price = 15000m, IsAvailable = false
        };
        _seats.Items.Add(_seat);
        _local = NewRequest(ReservationStatus.Pending);
        _requests.Items.Add(_local);

        _handler = new SyncReservationStatusCommandHandler(
            _requests, _seats, _booking, _clock,
            Options.Create(new ReservationSyncOptions { PendingTimeoutMinutes = 10 }),
            NullLogger<SyncReservationStatusCommandHandler>.Instance);
    }

    private SeatRequest NewRequest(ReservationStatus status) => new()
    {
        ReservationId = Guid.NewGuid(), SeatId = _seat.SeatId, EventId = _seat.EventId, SeatNumber = "A-1",
        UserId = Guid.NewGuid(), Price = 15000m, Status = status,
        RequestedAtUtc = _clock.GetUtcNow().UtcDateTime, CorrelationId = "corr"
    };

    private Task<ReservationStatusResult> Sync(Guid? id = null) =>
        _handler.Handle(new SyncReservationStatusCommand(id ?? _local.ReservationId), default);

    private void BookingSays(ReservationStatus status, DateTime? expires = null) =>
        _booking.Result = new BookingStatusInfo(status, expires, "x");

    // ---------- reserva inexistente / Booking caído ----------

    [Fact]
    public async Task Sync_UnknownLocalReservation_ThrowsNotFound_WithoutCallingBooking()
    {
        var ex = await Assert.ThrowsAsync<ReservationNotFoundException>(() => Sync(Guid.NewGuid()));

        Assert.Equal("reservation.not_found", ex.Code);
        Assert.Empty(_booking.Calls);
    }

    [Fact]
    public async Task Sync_BookingUnavailable_Propagates_AndWritesNothing_EvenIfOrphanByAge()
    {
        _clock.Advance(TimeSpan.FromMinutes(60)); // sería huérfana por antigüedad...
        _booking.Throw = new BookingUnavailableException("Booking caído");

        await Assert.ThrowsAsync<BookingUnavailableException>(() => Sync());

        // ...pero con Booking caído nunca se expira ni se libera nada.
        Assert.Equal(ReservationStatus.Pending, _local.Status);
        Assert.False(_seat.IsAvailable);
        Assert.Equal(0, _requests.SaveCalls);
    }

    // ---------- Booking no la conoce (404 -> null) ----------

    [Fact]
    public async Task Sync_BookingDoesNotKnowIt_YoungPending_StaysPending_WithoutWrites()
    {
        _clock.Advance(TimeSpan.FromMinutes(9));

        var result = await Sync();

        Assert.Equal(ReservationStatus.Pending, result.Status);
        Assert.Equal("La reserva aún no fue procesada por Booking", result.Message);
        Assert.Equal("A-1", result.SeatNumber);
        Assert.Null(result.ExpiresAtUtc);
        Assert.False(_seat.IsAvailable);
        Assert.Equal(0, _requests.SaveCalls);
    }

    [Fact]
    public async Task Sync_BookingDoesNotKnowIt_OldPending_IsOrphan_ExpiredAndSeatReleased()
    {
        _clock.Advance(TimeSpan.FromMinutes(11));

        var result = await Sync();

        Assert.Equal(ReservationStatus.Expired, result.Status);
        Assert.Equal("La reserva venció: Booking no llegó a procesarla", result.Message);
        Assert.Equal(ReservationStatus.Expired, _local.Status);
        Assert.True(_seat.IsAvailable);
        Assert.Equal(1, _requests.SaveCalls);
    }

    [Theory]
    [InlineData(ReservationStatus.Expired)]
    [InlineData(ReservationStatus.Rejected)]
    [InlineData(ReservationStatus.Retained)]
    [InlineData(ReservationStatus.Confirmed)]
    public async Task Sync_BookingDoesNotKnowIt_NonPendingLocal_IsLeftAsIs(ReservationStatus localStatus)
    {
        _local.Status = localStatus;
        _clock.Advance(TimeSpan.FromMinutes(60));

        var result = await Sync();

        Assert.Equal(localStatus, result.Status);
        Assert.Equal(localStatus, _local.Status);
        Assert.False(_seat.IsAvailable);
        Assert.Equal(0, _requests.SaveCalls);
    }

    // ---------- Booking informa un estado ----------

    [Fact]
    public async Task Sync_Retained_StoresStatus_KeepsSeatBlocked_AndReportsExpiry()
    {
        var expires = _clock.GetUtcNow().UtcDateTime.AddMinutes(5);
        BookingSays(ReservationStatus.Retained, expires);

        var result = await Sync();

        Assert.Equal(ReservationStatus.Retained, result.Status);
        Assert.Equal("Reserva retenida, pendiente de pago", result.Message);
        Assert.Equal(expires, result.ExpiresAtUtc);
        Assert.Equal(ReservationStatus.Retained, _local.Status);
        Assert.False(_seat.IsAvailable);
    }

    [Fact]
    public async Task Sync_Confirmed_StoresStatus_KeepsSeatBlocked_AndHidesExpiry()
    {
        BookingSays(ReservationStatus.Confirmed, DateTime.UtcNow.AddMinutes(5));

        var result = await Sync();

        Assert.Equal(ReservationStatus.Confirmed, _local.Status);
        Assert.Equal("Reserva confirmada", result.Message);
        Assert.Null(result.ExpiresAtUtc);
        Assert.False(_seat.IsAvailable);
    }

    [Theory]
    [InlineData(ReservationStatus.Rejected, "Reserva rechazada")]
    [InlineData(ReservationStatus.Expired, "La reserva venció")]
    public async Task Sync_RejectedOrExpired_StoresStatus_AndReleasesSeat(ReservationStatus status, string message)
    {
        BookingSays(status);

        var result = await Sync();

        Assert.Equal(status, result.Status);
        Assert.Equal(message, result.Message);
        Assert.Equal(status, _local.Status);
        Assert.True(_seat.IsAvailable);
        Assert.Equal(1, _requests.SaveCalls);
    }

    [Fact]
    public async Task Sync_UsesLocalSeatNumber_EvenIfBookingReportsOther()
    {
        _booking.Result = new BookingStatusInfo(ReservationStatus.Confirmed, null, "Z-9");

        Assert.Equal("A-1", (await Sync()).SeatNumber);
    }

    // ---------- liberación condicionada por otras solicitudes ----------

    [Theory]
    [InlineData(ReservationStatus.Pending)]
    [InlineData(ReservationStatus.Retained)]
    [InlineData(ReservationStatus.Confirmed)]
    public async Task Sync_Rejected_DoesNotReleaseSeat_WhenAnotherActiveRequestHoldsIt(ReservationStatus otherStatus)
    {
        _requests.Items.Add(NewRequest(otherStatus));
        BookingSays(ReservationStatus.Rejected);

        await Sync();

        Assert.Equal(ReservationStatus.Rejected, _local.Status);
        Assert.False(_seat.IsAvailable);
    }

    [Theory]
    [InlineData(ReservationStatus.Rejected)]
    [InlineData(ReservationStatus.Expired)]
    public async Task Sync_Rejected_ReleasesSeat_WhenOtherRequestsAreAlreadyFinal(ReservationStatus otherStatus)
    {
        _requests.Items.Add(NewRequest(otherStatus));
        BookingSays(ReservationStatus.Rejected);

        await Sync();

        Assert.True(_seat.IsAvailable);
    }

    [Fact]
    public async Task Sync_RequestForAnotherSeat_DoesNotBlockRelease()
    {
        var otherSeat = new Seat { SeatId = Guid.NewGuid(), EventId = _seat.EventId, SeatNumber = "B-1", IsAvailable = false };
        _seats.Items.Add(otherSeat);
        var otherRequest = NewRequest(ReservationStatus.Retained);
        otherRequest.SeatId = otherSeat.SeatId;
        _requests.Items.Add(otherRequest);
        BookingSays(ReservationStatus.Expired);

        await Sync();

        Assert.True(_seat.IsAvailable);
        Assert.False(otherSeat.IsAvailable);
    }

    // ---------- reversión tardía, idempotencia, datos inconsistentes ----------

    [Fact]
    public async Task Sync_LateReversal_OrphanExpiredThenBookingProcessesIt_BlocksSeatAgain()
    {
        _local.Status = ReservationStatus.Expired; // se había expirado como huérfana y liberado
        _seat.IsAvailable = true;
        BookingSays(ReservationStatus.Retained, DateTime.UtcNow.AddMinutes(5));

        var result = await Sync();

        Assert.Equal(ReservationStatus.Retained, result.Status);
        Assert.Equal(ReservationStatus.Retained, _local.Status);
        Assert.False(_seat.IsAvailable);
        Assert.Equal(1, _requests.SaveCalls);
    }

    [Fact]
    public async Task Sync_IsIdempotent_SecondCallWithSameBookingStatusWritesNothing()
    {
        BookingSays(ReservationStatus.Rejected);

        await Sync();
        await Sync();

        Assert.Equal(1, _requests.SaveCalls);
        Assert.True(_seat.IsAvailable);
    }

    [Fact]
    public async Task Sync_SteadyState_RetainedAgain_WritesNothing()
    {
        BookingSays(ReservationStatus.Retained, DateTime.UtcNow.AddMinutes(5));

        await Sync();
        var savesAfterFirst = _requests.SaveCalls;
        await Sync();

        Assert.Equal(savesAfterFirst, _requests.SaveCalls);
    }

    [Fact]
    public async Task Sync_SeatMissingLocally_StillUpdatesRequest_WithoutFailing()
    {
        _seats.Items.Clear();
        BookingSays(ReservationStatus.Rejected);

        var result = await Sync();

        Assert.Equal(ReservationStatus.Rejected, result.Status);
        Assert.Equal(ReservationStatus.Rejected, _local.Status);
        Assert.Equal(1, _requests.SaveCalls);
    }
}
