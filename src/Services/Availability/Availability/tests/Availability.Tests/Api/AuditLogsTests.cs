using System.Net.Http.Json;
using Availability.Infrastructure.Persistence;
using Availability.Tests.Support;
using Serilog.Events;

namespace Availability.Tests.Api;

/// <summary>Requisito 3.f de punta a punta: cada caso de uso deja INICIO y FIN con el CorrelationId de la request.</summary>
public class AuditLogsTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;
    private readonly HttpClient _client;

    public AuditLogsTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.Publisher.Reset();
        _factory.Booking.Reset();
        _client = factory.CreateAuthenticatedClient();
    }

    private static string NewId() => $"audit-{Guid.NewGuid():N}";

    private HttpRequestMessage Request(HttpMethod method, string url, string id, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Correlation-ID", id);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private IReadOnlyList<LogEvent> Audit(string id) =>
        _factory.Logs.For(id).Where(e => e.RenderMessage().StartsWith("INICIO:") || e.RenderMessage().StartsWith("FIN:")).ToList();

    [Fact]
    public async Task Reservation_LeavesInicioThenFin_ForTheUseCase_WithTheRequestCorrelationId()
    {
        var id = NewId();
        var seat = await _factory.CreateAvailableSeatAsync();

        await _client.SendAsync(Request(HttpMethod.Post, "/api/reservations", id,
            new { eventId = SeedData.EventId, seatNumber = seat, userId = TestJwt.ClientUserId, price = 15000m }));

        var audit = Audit(id);
        Assert.Equal(2, audit.Count);
        Assert.StartsWith("INICIO: RequestReservationCommand", audit[0].RenderMessage());
        Assert.Contains(seat, audit[0].RenderMessage()); // los datos de la solicitud quedan auditados
        Assert.StartsWith("FIN: RequestReservationCommand", audit[1].RenderMessage());
        Assert.DoesNotContain("error", audit[1].RenderMessage());
        Assert.All(audit, e => Assert.Equal(LogEventLevel.Information, e.Level));
    }

    [Fact]
    public async Task Catalog_LeavesInicioAndFin_EvenWithoutToken()
    {
        var id = NewId();

        await _factory.CreateClient().SendAsync(Request(HttpMethod.Get, "/api/seats", id));

        var audit = Audit(id);
        Assert.Equal(2, audit.Count);
        Assert.StartsWith("INICIO: GetSeatsQuery", audit[0].RenderMessage());
        Assert.StartsWith("FIN: GetSeatsQuery", audit[1].RenderMessage());
    }

    [Fact]
    public async Task StatusQuery_LeavesInicioAndFin_WithBusinessErrorCode_WhenReservationDoesNotExist()
    {
        var id = NewId();

        await _client.SendAsync(Request(HttpMethod.Get, $"/api/reservations/{Guid.NewGuid()}", id));

        var audit = Audit(id);
        Assert.StartsWith("INICIO: SyncReservationStatusCommand", audit[0].RenderMessage());
        Assert.Contains("error de negocio", audit[^1].RenderMessage());
        Assert.Contains("reservation.not_found", audit[^1].RenderMessage());
        Assert.Equal(LogEventLevel.Information, audit[^1].Level);
    }

    [Fact]
    public async Task TechnicalFailure_LeavesFinAsError()
    {
        var id = NewId();
        var seat = await _factory.CreateAvailableSeatAsync();
        _factory.Publisher.ThrowOnPublish = new Availability.Application.Exceptions.MessagePublishException("broker caído");

        await _client.SendAsync(Request(HttpMethod.Post, "/api/reservations", id,
            new { eventId = SeedData.EventId, seatNumber = seat, userId = TestJwt.ClientUserId, price = 15000m }));

        var fin = Audit(id).Last();
        Assert.StartsWith("FIN: RequestReservationCommand con error MessagePublishException", fin.RenderMessage());
        Assert.Equal(LogEventLevel.Error, fin.Level);
    }

    [Fact]
    public async Task TwoRequests_KeepTheirAuditTrailsSeparate()
    {
        var a = NewId();
        var b = NewId();

        await _factory.CreateClient().SendAsync(Request(HttpMethod.Get, "/api/seats", a));
        await _factory.CreateClient().SendAsync(Request(HttpMethod.Get, "/api/seats", b));

        Assert.Equal(2, Audit(a).Count);
        Assert.Equal(2, Audit(b).Count);
    }

    [Fact]
    public async Task RequestsRejectedBeforeTheUseCase_LeaveNoAuditEntries()
    {
        var id = NewId();

        await _factory.CreateClient().SendAsync(Request(HttpMethod.Get, $"/api/reservations/{Guid.NewGuid()}", id)); // 401

        Assert.Empty(Audit(id));
    }
}
