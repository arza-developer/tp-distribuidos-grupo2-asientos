using System.Net;
using System.Text;
using Availability.Application.Abstractions;
using Availability.Application.Exceptions;
using Availability.Domain.Enums;
using Availability.Infrastructure;
using Availability.Infrastructure.Bookings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Availability.Tests.Bookings;

public class BookingClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return respond(request, ct);
        }
    }

    private sealed class FakeCorrelation(string id = "corr-7") : ICorrelationIdAccessor
    {
        public string CorrelationId { get; } = id;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (BookingClient Client, StubHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://booking.test/"), Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        return (new BookingClient(http, new FakeCorrelation(), NullLogger<BookingClient>.Instance), handler);
    }

    private static (BookingClient Client, StubHandler Handler) CreateReturning(HttpStatusCode status, string json) =>
        Create((_, _) => Task.FromResult(Json(status, json)));

    [Theory]
    [InlineData("Retained", ReservationStatus.Retained)]
    [InlineData("Confirmed", ReservationStatus.Confirmed)]
    [InlineData("Rejected", ReservationStatus.Rejected)]
    [InlineData("Expired", ReservationStatus.Expired)]
    [InlineData("retained", ReservationStatus.Retained)]
    public async Task GetStatus_MapsBookingStatusToEnum(string bookingStatus, ReservationStatus expected)
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, $$"""{"reservationId":"{{Guid.NewGuid()}}","seatNumber":"A-1","status":"{{bookingStatus}}"}""");

        var info = await client.GetStatusAsync(Guid.NewGuid());

        Assert.Equal(expected, info!.Status);
    }

    [Fact]
    public async Task GetStatus_ParsesSeatNumberAndExpiry_AcceptingPascalCaseJson()
    {
        var expires = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var (client, _) = CreateReturning(HttpStatusCode.OK,
            $$"""{"ReservationId":"{{Guid.NewGuid()}}","SeatNumber":"B-1","Status":"Retained","ExpiresAtUtc":"{{expires:O}}"}""");

        var info = await client.GetStatusAsync(Guid.NewGuid());

        Assert.Equal("B-1", info!.SeatNumber);
        Assert.Equal(expires, info.ExpiresAtUtc!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task GetStatus_WithoutExpiry_ReturnsNullExpiry()
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, """{"seatNumber":"A-1","status":"Confirmed"}""");

        Assert.Null((await client.GetStatusAsync(Guid.NewGuid()))!.ExpiresAtUtc);
    }

    [Fact]
    public async Task GetStatus_BookingReturns404_ReturnsNull_NotYetProcessed()
    {
        var (client, _) = CreateReturning(HttpStatusCode.NotFound, "{}");

        Assert.Null(await client.GetStatusAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GetStatus_BookingErrorStatus_ThrowsBookingUnavailable(HttpStatusCode status)
    {
        var (client, _) = CreateReturning(status, "{}");

        await Assert.ThrowsAsync<BookingUnavailableException>(() => client.GetStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetStatus_NetworkFailure_ThrowsBookingUnavailable_KeepingInnerException()
    {
        var inner = new HttpRequestException("connection refused");
        var (client, _) = Create((_, _) => throw inner);

        var ex = await Assert.ThrowsAsync<BookingUnavailableException>(() => client.GetStatusAsync(Guid.NewGuid()));

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public async Task GetStatus_HttpClientTimeout_ThrowsBookingUnavailable()
    {
        var (client, _) = Create(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct); // el HttpClient cancela por Timeout
            return Json(HttpStatusCode.OK, "{}");
        }, timeout: TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAsync<BookingUnavailableException>(() => client.GetStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetStatus_CallerCancellation_IsPropagated_NotWrapped()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (client, _) = Create(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(HttpStatusCode.OK, "{}");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetStatusAsync(Guid.NewGuid(), cts.Token));
    }

    [Theory]
    [InlineData("""{"seatNumber":"A-1","status":"Pagada"}""")]
    [InlineData("""{"seatNumber":"A-1"}""")]
    [InlineData("""{"seatNumber":"A-1","status":null}""")]
    [InlineData("esto no es json")]
    [InlineData("null")]
    public async Task GetStatus_UnrecognizableBody_ThrowsBookingUnavailable(string body)
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, body);

        await Assert.ThrowsAsync<BookingUnavailableException>(() => client.GetStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetStatus_SendsGetToExactUrl_WithCorrelationHeader()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateReturning(HttpStatusCode.OK, """{"seatNumber":"A-1","status":"Retained"}""");

        await client.GetStatusAsync(id);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"http://booking.test/api/bookings/{id}", request.RequestUri!.ToString());
        Assert.Equal("corr-7", Assert.Single(request.Headers.GetValues("X-Correlation-ID")));
    }

    [Fact]
    public void BookingOptions_HaveSafeDefaults()
    {
        var options = new BookingOptions();

        Assert.Equal("http://localhost:5102", options.BaseUrl);
        Assert.Equal(5, options.TimeoutSeconds);
    }

    [Fact]
    public void AddInfrastructure_RegistersTypedBookingClient_BoundToConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Booking:BaseUrl"] = "http://booking-api:5102",
                ["Booking:TimeoutSeconds"] = "9"
            }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ICorrelationIdAccessor>(new FakeCorrelation());
        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<BookingClient>(scope.ServiceProvider.GetRequiredService<IBookingClient>());
        var options = scope.ServiceProvider.GetRequiredService<IOptions<BookingOptions>>().Value;
        Assert.Equal("http://booking-api:5102", options.BaseUrl);
        Assert.Equal(9, options.TimeoutSeconds);
    }

    [Fact]
    public async Task GetStatus_ParsesTheExactJsonBookingEmits_IncludingRejectionReason()
    {
        // Muestra con la forma que arma BookingsController de Booking (propiedades anónimas serializadas en camelCase).
        const string bookingJson =
            """{"reservationId":"6dd8bbba-dc01-4b06-8f37-f96c6177a2bd","seatNumber":"A-2","status":"Rejected","expiresAtUtc":null,"rejectionReason":"SeatOccupied"}""";
        var (client, _) = CreateReturning(HttpStatusCode.OK, bookingJson);

        var info = await client.GetStatusAsync(Guid.NewGuid());

        Assert.Equal(ReservationStatus.Rejected, info!.Status);
        Assert.Equal("A-2", info.SeatNumber);
        Assert.Equal("SeatOccupied", info.RejectionReason);
    }

    [Fact]
    public async Task GetStatus_WithoutRejectionReason_LeavesItNull()
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, """{"seatNumber":"A-1","status":"Retained","rejectionReason":null}""");

        Assert.Null((await client.GetStatusAsync(Guid.NewGuid()))!.RejectionReason);
    }

    [Fact]
    public async Task GetStatus_StatusOutsideBookingVocabulary_IsATechnicalFailure()
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, """{"seatNumber":"A-1","status":"Retenida"}""");

        await Assert.ThrowsAsync<BookingUnavailableException>(() => client.GetStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetStatus_PropagatesTheCorrelationIdToBooking()
    {
        var (client, handler) = CreateReturning(HttpStatusCode.OK, """{"seatNumber":"A-1","status":"Retained"}""");

        await client.GetStatusAsync(Guid.NewGuid());

        Assert.Equal("corr-7", Assert.Single(Assert.Single(handler.Requests).Headers.GetValues("X-Correlation-ID")));
    }
}
