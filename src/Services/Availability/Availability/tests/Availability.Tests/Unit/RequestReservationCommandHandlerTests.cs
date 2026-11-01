using Availability.Application.Abstractions;
using Availability.Application.Exceptions;
using Availability.Application.Messaging;
using Availability.Application.Reservations.Commands.RequestReservation;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Domain.Exceptions;
using Availability.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using SeatsReservation.Contracts.Events;

namespace Availability.Tests.Unit;

public class RequestReservationCommandHandlerTests
{
    private sealed class FakePublisher(InMemorySeatRequestRepository requests) : IMessagePublisher
    {
        public List<(object Message, string Queue)> Published { get; } = [];
        public Exception? ThrowOnPublish { get; set; }
        public int PersistedCountAtPublish { get; private set; } = -1;

        public Task PublishAsync<T>(T message, string topicOrQueue, CancellationToken ct = default)
        {
            PersistedCountAtPublish = requests.Items.Count;
            if (ThrowOnPublish is not null) throw ThrowOnPublish;
            Published.Add((message!, topicOrQueue));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCorrelation(string id = "corr-42") : ICorrelationIdAccessor
    {
        public string CorrelationId { get; } = id;
    }

    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid CurrentUserId = Guid.NewGuid();

    private readonly InMemorySeatRepository _seats = new();
    private readonly InMemorySeatRequestRepository _requests = new();
    private readonly FakePublisher _publisher;
    private readonly RequestReservationCommandHandler _handler;

    public RequestReservationCommandHandlerTests()
    {
        _seats.Items.Add(new Seat { SeatId = Guid.NewGuid(), EventId = EventId, SeatNumber = "A-1", Price = 15000m, IsAvailable = true });
        _seats.Items.Add(new Seat { SeatId = Guid.NewGuid(), EventId = EventId, SeatNumber = "A-2", Price = 15000m, IsAvailable = true });
        _seats.Items.Add(new Seat { SeatId = Guid.NewGuid(), EventId = EventId, SeatNumber = "A-3", Price = 18000m, IsAvailable = false });
        _publisher = new FakePublisher(_requests);
        _handler = new RequestReservationCommandHandler(
            _seats, _requests, _publisher, new FakeCorrelation(), new FakeCurrentUserAccessor(CurrentUserId), TimeProvider.System,
            NullLogger<RequestReservationCommandHandler>.Instance);
    }

    private static RequestReservationCommand Cmd(string seat = "A-1", decimal price = 15000m, Guid? eventId = null, Guid? userId = null) =>
        new(eventId ?? EventId, seat, userId ?? CurrentUserId, price);

    [Fact]
    public async Task Handle_Success_PersistsPendingRequest_AndPublishesEventToBookingQueue()
    {
        var command = Cmd();

        var result = await _handler.Handle(command, default);

        var stored = Assert.Single(_requests.Items);
        Assert.Equal(result.ReservationId, stored.ReservationId);
        Assert.Equal(ReservationStatus.Pending, stored.Status);
        Assert.Equal(_seats.Items[0].SeatId, stored.SeatId);
        Assert.Equal("corr-42", stored.CorrelationId);

        var (message, queue) = Assert.Single(_publisher.Published);
        Assert.Equal(QueueNames.BookingRequests, queue);
        var evt = Assert.IsType<ReservationRequestedEvent>(message);
        Assert.Equal(result.ReservationId, evt.ReservationId);
        Assert.Equal(EventId, evt.EventId);
        Assert.Equal("A-1", evt.SeatNumber);
        Assert.Equal(command.UserId, evt.UserId);
        Assert.Equal("corr-42", evt.CorrelationId);
    }

    [Fact]
    public async Task Handle_Success_PersistsBeforePublishing()
    {
        await _handler.Handle(Cmd(), default);

        Assert.Equal(1, _publisher.PersistedCountAtPublish);
    }

    [Fact]
    public async Task Handle_PublishesCatalogPrice_NotTheRequestedOne()
    {
        await _handler.Handle(Cmd(price: 1m), default);

        var evt = Assert.IsType<ReservationRequestedEvent>(Assert.Single(_publisher.Published).Message);
        Assert.Equal(15000m, evt.Price);
        Assert.Equal(15000m, Assert.Single(_requests.Items).Price);
    }

    [Fact]
    public async Task Handle_TrimsSeatNumber()
    {
        await _handler.Handle(Cmd(seat: "  A-1 "), default);

        Assert.Equal("A-1", Assert.Single(_requests.Items).SeatNumber);
    }

    [Fact]
    public async Task Handle_UnavailableSeat_ThrowsSeatUnavailable_AndPersistsAndPublishesNothing()
    {
        var ex = await Assert.ThrowsAsync<SeatUnavailableException>(
            () => _handler.Handle(Cmd(seat: "A-3", price: 18000m), default));

        Assert.Equal("seat.unavailable", ex.Code);
        Assert.Empty(_requests.Items);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Handle_Success_MarksSeatAsUnavailable()
    {
        await _handler.Handle(Cmd(seat: "A-1"), default);

        Assert.False(_seats.Items.Single(s => s.SeatNumber == "A-1").IsAvailable);
    }

    [Fact]
    public async Task Handle_SecondRequestForSameSeat_ThrowsSeatUnavailable()
    {
        await _handler.Handle(Cmd(seat: "A-1"), default);

        await Assert.ThrowsAsync<SeatUnavailableException>(() => _handler.Handle(Cmd(seat: "A-1"), default));

        Assert.Single(_requests.Items);
        Assert.Single(_publisher.Published);
    }

    [Fact]
    public async Task Handle_UnknownSeat_ThrowsSeatNotFound_AndPersistsAndPublishesNothing()
    {
        await Assert.ThrowsAsync<SeatNotFoundException>(() => _handler.Handle(Cmd(seat: "Z-9"), default));

        Assert.Empty(_requests.Items);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Handle_UnknownEvent_ThrowsSeatNotFound()
    {
        await Assert.ThrowsAsync<SeatNotFoundException>(() => _handler.Handle(Cmd(eventId: Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_PublishFails_RemovesRequest_AndRethrowsTechnicalException()
    {
        _publisher.ThrowOnPublish = new MessagePublishException("broker caído");

        await Assert.ThrowsAsync<MessagePublishException>(() => _handler.Handle(Cmd(), default));

        Assert.Empty(_requests.Items);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Handle_PublishFails_RestoresSeatAvailability_SoItCanBeRequestedAgain()
    {
        _publisher.ThrowOnPublish = new MessagePublishException("broker caído");

        await Assert.ThrowsAsync<MessagePublishException>(() => _handler.Handle(Cmd(seat: "A-1"), default));

        Assert.True(_seats.Items.Single(s => s.SeatNumber == "A-1").IsAvailable);

        _publisher.ThrowOnPublish = null; // el broker se recupera
        var retry = await _handler.Handle(Cmd(seat: "A-1"), default);

        Assert.NotEqual(Guid.Empty, retry.ReservationId);
        Assert.False(_seats.Items.Single(s => s.SeatNumber == "A-1").IsAvailable);
    }

    [Fact]
    public async Task Handle_PublishFailsWithOtherException_DoesNotCompensate()
    {
        _publisher.ThrowOnPublish = new InvalidOperationException("inesperado");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(Cmd(), default));

        Assert.Single(_requests.Items);
    }

    [Theory]
    [InlineData("", "seat")]
    [InlineData("   ", "seat")]
    public async Task Handle_BlankSeatNumber_ThrowsInvalidRequest(string seat, string _)
    {
        await Assert.ThrowsAsync<InvalidReservationRequestException>(() => _handler.Handle(Cmd(seat: seat), default));
        Assert.Empty(_requests.Items);
    }

    [Fact]
    public async Task Handle_EmptyEventId_ThrowsInvalidRequest() =>
        await Assert.ThrowsAsync<InvalidReservationRequestException>(
            () => _handler.Handle(new RequestReservationCommand(Guid.Empty, "A-1", Guid.NewGuid(), 1m), default));

    [Fact]
    public async Task Handle_EmptyUserId_ThrowsInvalidRequest() =>
        await Assert.ThrowsAsync<InvalidReservationRequestException>(
            () => _handler.Handle(new RequestReservationCommand(EventId, "A-1", Guid.Empty, 1m), default));

    [Fact]
    public async Task Handle_EachCallGetsADistinctReservationId()
    {
        var first = await _handler.Handle(Cmd(seat: "A-1"), default);
        var second = await _handler.Handle(Cmd(seat: "A-2"), default);

        Assert.NotEqual(first.ReservationId, second.ReservationId);
    }

    // ---------- identidad (TD-4): el userId del body debe ser el del token ----------

    [Fact]
    public async Task Handle_UserIdDifferentFromAuthenticatedUser_ThrowsMismatch_AndTouchesNothing()
    {
        var other = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ReservationUserMismatchException>(() => _handler.Handle(Cmd(userId: other), default));

        Assert.Equal("reservation.user_mismatch", ex.Code);
        Assert.Empty(_requests.Items);
        Assert.Empty(_publisher.Published);
        Assert.True(_seats.Items.Single(s => s.SeatNumber == "A-1").IsAvailable); // el asiento no se bloquea
    }

    [Fact]
    public async Task Handle_NoAuthenticatedUser_ThrowsMismatch()
    {
        var handler = new RequestReservationCommandHandler(
            _seats, _requests, _publisher, new FakeCorrelation(), new FakeCurrentUserAccessor(null), TimeProvider.System,
            NullLogger<RequestReservationCommandHandler>.Instance);

        await Assert.ThrowsAsync<ReservationUserMismatchException>(() => handler.Handle(Cmd(), default));

        Assert.Empty(_requests.Items);
    }

    [Fact]
    public async Task Handle_StoresTheAuthenticatedUserAsOwner()
    {
        await _handler.Handle(Cmd(), default);

        Assert.Equal(CurrentUserId, Assert.Single(_requests.Items).UserId);
    }
}
