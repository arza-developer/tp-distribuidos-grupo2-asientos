using System.Diagnostics;
using System.Text.Json;
using Availability.Application.Exceptions;
using Availability.Application.Messaging;
using Availability.Infrastructure.Messaging;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeatsReservation.Contracts.Events;

namespace Availability.Tests.Messaging;

public class AzureQueuePublisherTests
{
    private const string AzuriteKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private static AzureQueuePublisher CreatePublisher(string connectionString) =>
        new(Options.Create(new AzureQueueOptions { ConnectionString = connectionString, MaxRetries = 1, NetworkTimeoutSeconds = 2 }),
            NullLogger<AzureQueuePublisher>.Instance);

    private static ReservationRequestedEvent NewEvent() => new()
    {
        ReservationId = Guid.NewGuid(), EventId = Guid.NewGuid(), SeatNumber = "A-1",
        UserId = Guid.NewGuid(), Price = 15000m, CorrelationId = "corr-123"
    };

    [Fact]
    public void QueueNames_BookingRequests_MatchesContract() =>
        Assert.Equal("booking-requests-queue", QueueNames.BookingRequests);

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
    public async Task Publish_UnreachableBroker_ThrowsMessagePublishException_Quickly()
    {
        var publisher = CreatePublisher(
            $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={AzuriteKey};QueueEndpoint=http://127.0.0.1:1/devstoreaccount1;");
        var sw = Stopwatch.StartNew();

        var ex = await Assert.ThrowsAsync<MessagePublishException>(
            () => publisher.PublishAsync(NewEvent(), QueueNames.BookingRequests));

        Assert.NotNull(ex.InnerException);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), $"Tardó demasiado: {sw.Elapsed}");
    }

    [Fact]
    public async Task Publish_InvalidConnectionString_ThrowsMessagePublishException()
    {
        var publisher = CreatePublisher("esto-no-es-una-connection-string");

        await Assert.ThrowsAsync<MessagePublishException>(
            () => publisher.PublishAsync(NewEvent(), QueueNames.BookingRequests));
    }

    [AzuriteFact]
    public async Task Publish_Azurite_MessageArrivesInQueueAsBase64Json()
    {
        var queueName = $"test-{Guid.NewGuid():N}";
        var publisher = CreatePublisher(AzuriteFactAttribute.ConnectionString);
        var evt = NewEvent();
        var reader = new QueueClient(AzuriteFactAttribute.ConnectionString, queueName,
            new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

        try
        {
            await publisher.PublishAsync(evt, queueName);
            await publisher.PublishAsync(evt, queueName); // la cola ya existe: no debe fallar

            var received = (await reader.ReceiveMessagesAsync(maxMessages: 2)).Value;

            Assert.Equal(2, received.Length);
            Assert.Equal(evt, QueueMessageSerializer.Deserialize<ReservationRequestedEvent>(received[0].MessageText));
        }
        finally
        {
            await reader.DeleteIfExistsAsync();
        }
    }
}
