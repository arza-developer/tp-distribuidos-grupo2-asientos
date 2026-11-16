using System.Diagnostics;
using System.Text.Json;
using Availability.Application.Exceptions;
using Availability.Application.Messaging;
using Availability.Infrastructure.Messaging;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeatsReservation.Contracts.Events;

namespace Availability.Tests.Messaging;

public class ServiceBusPublisherTests
{
    /// <summary>Sender falso escrito a mano (patrón de FakeTimeProvider): registra lo enviado o lanza la excepción configurada.</summary>
    private sealed class FakeSender : ServiceBusSender
    {
        public List<ServiceBusMessage> Sent { get; } = [];
        public Exception? Throw { get; set; }

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            if (Throw is not null) throw Throw;
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static ReservationRequestedEvent NewEvent() => new()
    {
        ReservationId = Guid.NewGuid(), EventId = Guid.NewGuid(), SeatNumber = "A-1",
        UserId = Guid.NewGuid(), Price = 15000m, CorrelationId = "corr-123"
    };

    private static ServiceBusPublisher WithSender(FakeSender sender, List<string>? topics = null) =>
        new(topic => { topics?.Add(topic); return sender; }, NullLogger<ServiceBusPublisher>.Instance);

    private static ServiceBusPublisher Real(string connectionString) =>
        new(Options.Create(new ServiceBusOptions { ConnectionString = connectionString, MaxRetries = 1, TryTimeoutSeconds = 2 }),
            NullLogger<ServiceBusPublisher>.Instance);

    [Fact]
    public void Topic_MatchesBookingContract() =>
        Assert.Equal("grupo2-booking-topic", MessagingNames.BookingRequestsTopic);

    [Fact]
    public void Serializer_WritesCamelCaseJson_AndRoundTrips()
    {
        var evt = NewEvent();

        var json = QueueMessageSerializer.Serialize(evt);

        using var doc = JsonDocument.Parse(json);
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["reservationId", "eventId", "seatNumber", "userId", "price", "requestedAtUtc", "correlationId"], names);
        Assert.Equal(evt, QueueMessageSerializer.Deserialize<ReservationRequestedEvent>(json));
    }

    [Fact]
    public async Task Publish_SendsJsonMessageWithIdsAndCorrelation_ToRequestedTopic()
    {
        var sender = new FakeSender();
        var topics = new List<string>();
        var publisher = WithSender(sender, topics);
        var evt = NewEvent();

        await publisher.PublishAsync(evt, MessagingNames.BookingRequestsTopic);
        await publisher.PublishAsync(evt, MessagingNames.BookingRequestsTopic);

        Assert.Equal([MessagingNames.BookingRequestsTopic], topics); // un sender por topic (cacheado)
        Assert.Equal(2, sender.Sent.Count);
        var sent = sender.Sent[0];
        Assert.Equal("application/json", sent.ContentType);
        Assert.Equal(evt.ReservationId.ToString(), sent.MessageId);
        Assert.Equal("corr-123", sent.CorrelationId);
        Assert.Equal("corr-123", sent.ApplicationProperties["CorrelationId"]);
        Assert.Equal(evt, QueueMessageSerializer.Deserialize<ReservationRequestedEvent>(sent.Body.ToString()));
    }

    [Fact]
    public async Task Publish_SenderFails_ThrowsMessagePublishException_WithInnerException()
    {
        var failure = new ServiceBusException("broker caído", ServiceBusFailureReason.ServiceCommunicationProblem);
        var publisher = WithSender(new FakeSender { Throw = failure });

        var ex = await Assert.ThrowsAsync<MessagePublishException>(
            () => publisher.PublishAsync(NewEvent(), MessagingNames.BookingRequestsTopic));

        Assert.Same(failure, ex.InnerException);
    }

    [Fact]
    public async Task Publish_Cancelled_PropagatesCancellation_NotWrapped()
    {
        var publisher = WithSender(new FakeSender { Throw = new OperationCanceledException() });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => publisher.PublishAsync(NewEvent(), MessagingNames.BookingRequestsTopic));
    }

    [Fact]
    public async Task Publish_NoConnectionString_ThrowsMessagePublishException_NotAtStartup()
    {
        var publisher = Real(string.Empty); // construir no falla: el error aparece recién al publicar

        await Assert.ThrowsAsync<MessagePublishException>(
            () => publisher.PublishAsync(NewEvent(), MessagingNames.BookingRequestsTopic));
    }

    [Fact]
    public async Task Publish_InvalidConnectionString_ThrowsMessagePublishException()
    {
        var publisher = Real("esto-no-es-una-connection-string");

        await Assert.ThrowsAsync<MessagePublishException>(
            () => publisher.PublishAsync(NewEvent(), MessagingNames.BookingRequestsTopic));
    }

    [Fact]
    public async Task Publish_UnreachableBroker_ThrowsMessagePublishException_Quickly()
    {
        var publisher = Real("Endpoint=sb://127.0.0.1:1/;SharedAccessKeyName=k;SharedAccessKey=YWJj;EntityPath=grupo2-booking-topic");
        var sw = Stopwatch.StartNew();

        await Assert.ThrowsAsync<MessagePublishException>(
            () => publisher.PublishAsync(NewEvent(), MessagingNames.BookingRequestsTopic));

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"Tardó demasiado: {sw.Elapsed}");
    }
}
