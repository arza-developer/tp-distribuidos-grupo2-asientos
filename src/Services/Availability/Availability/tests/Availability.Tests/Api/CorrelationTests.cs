using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Availability.Application.Abstractions.IRepository;
using Availability.Infrastructure.Persistence;
using Availability.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using SeatsReservation.Contracts.Events;

namespace Availability.Tests.Api;

/// <summary>Requisito 3.f: X-Correlation-ID entrante o generado, devuelto en la respuesta y presente en eventos y logs.</summary>
public class CorrelationTests : IClassFixture<AvailabilityApiFactory>
{
    private const string Header = "X-Correlation-ID";

    private readonly AvailabilityApiFactory _factory;
    private readonly HttpClient _client;

    public CorrelationTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.Publisher.Reset();
        _factory.Booking.Reset();
        _client = factory.CreateAuthenticatedClient();
    }

    private static string NewId() => $"test-{Guid.NewGuid():N}";

    private static HttpRequestMessage Request(HttpMethod method, string url, string? correlationId, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (correlationId is not null) request.Headers.Add(Header, correlationId);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private static string? ResponseId(HttpResponseMessage r) =>
        r.Headers.TryGetValues(Header, out var values) ? values.Single() : null;

    private async Task<object> ReservationBodyAsync() => new
    {
        eventId = SeedData.EventId,
        seatNumber = await _factory.CreateAvailableSeatAsync(),
        userId = TestJwt.ClientUserId,
        price = 15000m
    };

    // ---------- header ----------

    [Fact]
    public async Task IncomingCorrelationId_IsEchoedInTheResponse()
    {
        var id = NewId();

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/health", id));

        Assert.Equal(id, ResponseId(response));
    }

    [Fact]
    public async Task WithoutHeader_AnIdIsGeneratedPerRequest()
    {
        var first = ResponseId(await _client.GetAsync("/health"));
        var second = ResponseId(await _client.GetAsync("/health"));

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("tiene espacios")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("a-muy-largo-a-muy-largo-a-muy-largo-a-muy-largo-a-muy-largo-a-muy-largo-a-muy-largo")]
    public async Task UnsafeOrTooLongIncomingId_IsReplacedByAGeneratedOne(string unsafeId)
    {
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/health", unsafeId));

        var returned = ResponseId(response);
        Assert.False(string.IsNullOrWhiteSpace(returned));
        Assert.NotEqual(unsafeId, returned);
    }

    [Fact]
    public async Task TheIdSurvivesEveryKindOfResponse()
    {
        var id = NewId();
        var anonymous = _factory.CreateClient();

        // 401 (challenge), 404 de negocio (excepción manejada), 404 de ruta y 500 inesperado
        Assert.Equal(id, ResponseId(await anonymous.SendAsync(Request(HttpMethod.Get, $"/api/reservations/{Guid.NewGuid()}", id))));
        Assert.Equal(id, ResponseId(await _client.SendAsync(Request(HttpMethod.Get, $"/api/reservations/{Guid.NewGuid()}", id))));
        Assert.Equal(id, ResponseId(await _client.SendAsync(Request(HttpMethod.Get, "/api/no-existe", id))));

        _factory.Publisher.ThrowOnPublish = new InvalidOperationException("detalle-inesperado");
        var failed = await _client.SendAsync(Request(HttpMethod.Post, "/api/reservations", id, await ReservationBodyAsync()));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(id, ResponseId(failed));
    }

    // ---------- propagación al evento y a la solicitud local ----------

    [Fact]
    public async Task TheIdIsPropagatedToThePublishedEventAndTheLocalRequest()
    {
        var id = NewId();

        var response = await _client.SendAsync(Request(HttpMethod.Post, "/api/reservations", id, await ReservationBodyAsync()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(id, ResponseId(response));
        var reservationId = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("reservationId").GetGuid();

        var evt = Assert.IsType<ReservationRequestedEvent>(Assert.Single(_factory.Publisher.Published).Message);
        Assert.Equal(id, evt.CorrelationId);

        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ISeatRequestRepository>()
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId);
        Assert.Equal(id, stored!.CorrelationId);
    }

    // ---------- logs ----------

    [Fact]
    public async Task LogsOfARequest_CarryItsCorrelationId()
    {
        var id = NewId();

        await _client.SendAsync(Request(HttpMethod.Get, $"/api/reservations/{Guid.NewGuid()}", id)); // 404 de negocio

        var events = _factory.Logs.For(id);
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.RenderMessage().Contains("reservation.not_found"));
    }

    [Fact]
    public async Task LogsOfDifferentRequests_AreNotMixed()
    {
        var a = NewId();
        var b = NewId();

        await _client.SendAsync(Request(HttpMethod.Get, $"/api/reservations/{Guid.NewGuid()}", a));
        await _client.SendAsync(Request(HttpMethod.Get, $"/api/reservations/{Guid.NewGuid()}", b));

        Assert.NotEmpty(_factory.Logs.For(a));
        Assert.NotEmpty(_factory.Logs.For(b));
        Assert.DoesNotContain(_factory.Logs.For(a), e => _factory.Logs.For(b).Contains(e));
    }

    [Fact]
    public async Task HandledBusinessExceptions_AreNotRepeatedAsFailByTheExceptionMiddleware()
    {
        var id = NewId();
        var seat = await _factory.CreateAvailableSeatAsync();
        var body = new { eventId = SeedData.EventId, seatNumber = seat, userId = TestJwt.ClientUserId, price = 15000m };
        await _client.SendAsync(Request(HttpMethod.Post, "/api/reservations", id, body));

        // 409 de negocio (asiento ya reservado) y 404 (asiento inexistente)
        await _client.SendAsync(Request(HttpMethod.Post, "/api/reservations", id, body));
        await _client.SendAsync(Request(HttpMethod.Post, "/api/reservations", id,
            new { eventId = SeedData.EventId, seatNumber = "Z-9", userId = TestJwt.ClientUserId, price = 1m }));

        var noise = _factory.Logs.For(id).Where(e =>
            e.Level == LogEventLevel.Error && e.Properties.TryGetValue("SourceContext", out var s)
            && s.ToString().Contains("ExceptionHandlerMiddleware")).ToList();
        Assert.Empty(noise);
        Assert.Contains(_factory.Logs.For(id), e => e.RenderMessage().Contains("seat.unavailable"));
    }

    [Fact]
    public async Task UnexpectedErrors_AreStillLoggedAsErrors_WithTheCorrelationId()
    {
        var id = NewId();
        _factory.Publisher.ThrowOnPublish = new InvalidOperationException("detalle-inesperado");

        await _client.SendAsync(Request(HttpMethod.Post, "/api/reservations", id, await ReservationBodyAsync()));

        Assert.Contains(_factory.Logs.For(id), e =>
            e.Level == LogEventLevel.Error && e.Exception is InvalidOperationException);
    }
}
