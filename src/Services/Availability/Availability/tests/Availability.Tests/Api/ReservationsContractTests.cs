using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Availability.Application.Abstractions.IRepository;
using Availability.Application.Exceptions;
using Availability.Application.Messaging;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using SeatsReservation.Contracts.Events;

namespace Availability.Tests.Api;

/// <summary>
/// Contrato público de POST /api/reservations (heredado de Availability.Api).
/// La fábrica es compartida por la clase y una reserva exitosa deja el asiento no disponible, por eso
/// cada test crea su propio asiento (<see cref="CreateAvailableSeatAsync"/>) y son independientes entre sí.
/// </summary>
public class ReservationsContractTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;
    private readonly HttpClient _client;

    public ReservationsContractTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.Publisher.Reset();
        _client = factory.CreateClient();
    }

    private async Task<string> CreateAvailableSeatAsync()
    {
        var number = $"T-{Guid.NewGuid():N}"[..12];
        using var scope = _factory.Services.CreateScope();
        var seats = scope.ServiceProvider.GetRequiredService<ISeatRepository>();
        await seats.AddAsync(new Seat
        {
            SeatId = Guid.NewGuid(), EventId = SeedData.EventId, SeatNumber = number, Price = 15000m, IsAvailable = true
        });
        await seats.SaveChangesAsync();
        return number;
    }

    private static object Body(string? seat, Guid? userId = null, Guid? eventId = null, decimal price = 15000m) => new
    {
        eventId = eventId ?? SeedData.EventId,
        seatNumber = seat,
        userId = userId ?? Guid.NewGuid(),
        price
    };

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<int> CountRequestsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ISeatRequestRepository>().ListAsync()).Count;
    }

    [Fact]
    public async Task Post_ValidRequest_Returns202WithContractBody()
    {
        var seat = await CreateAvailableSeatAsync();

        var response = await _client.PostAsJsonAsync("/api/reservations", Body(seat));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await ReadAsync(response);
        Assert.Equal(["reservationId", "status", "message", "checkStatusUrl"], json.EnumerateObject().Select(p => p.Name));
        var id = json.GetProperty("reservationId").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Pending", json.GetProperty("status").GetString());
        Assert.Equal("Solicitud recibida y encolada para procesamiento", json.GetProperty("message").GetString());
        Assert.Equal($"/api/reservations/{id}", json.GetProperty("checkStatusUrl").GetString());
    }

    [Fact]
    public async Task Post_ValidRequest_PublishesEvent_AndPersistsPendingRequest()
    {
        var seat = await CreateAvailableSeatAsync();
        var userId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync("/api/reservations", Body(seat, userId: userId, price: 1m));
        var id = (await ReadAsync(response)).GetProperty("reservationId").GetGuid();

        var (message, queue) = Assert.Single(_factory.Publisher.Published);
        Assert.Equal(QueueNames.BookingRequests, queue);
        var evt = Assert.IsType<ReservationRequestedEvent>(message);
        Assert.Equal(id, evt.ReservationId);
        Assert.Equal(SeedData.EventId, evt.EventId);
        Assert.Equal(seat, evt.SeatNumber);
        Assert.Equal(userId, evt.UserId);
        Assert.Equal(15000m, evt.Price); // precio del catálogo, no el del body
        Assert.False(string.IsNullOrEmpty(evt.CorrelationId));

        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ISeatRequestRepository>()
            .FirstOrDefaultAsync(r => r.ReservationId == id);
        Assert.NotNull(stored);
        Assert.Equal(ReservationStatus.Pending, stored!.Status);
    }

    [Fact]
    public async Task Post_SecondRequestForSameSeat_IsRejectedAsUnavailable_AndSeatShowsAsUnavailableInCatalog()
    {
        var seat = await CreateAvailableSeatAsync();
        var first = await _client.PostAsJsonAsync("/api/reservations", Body(seat));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/reservations", Body(seat));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("seat.unavailable", (await ReadAsync(second)).GetProperty("code").GetString());
        Assert.Single(_factory.Publisher.Published);

        var catalog = await ReadAsync(await _client.GetAsync($"/api/seats?eventId={SeedData.EventId}"));
        var entry = catalog.EnumerateArray().Single(s => s.GetProperty("seatNumber").GetString() == seat);
        Assert.False(entry.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task Post_UnknownSeat_Returns404WithCode_AndNothingPersistedOrPublished()
    {
        var before = await CountRequestsAsync();

        var response = await _client.PostAsJsonAsync("/api/reservations", Body("Z-9"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("seat.not_found", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Empty(_factory.Publisher.Published);
        Assert.Equal(before, await CountRequestsAsync());
    }

    [Fact]
    public async Task Post_EmptyUserId_Returns400WithCode()
    {
        var seat = await CreateAvailableSeatAsync();

        var response = await _client.PostAsJsonAsync("/api/reservations", Body(seat, userId: Guid.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("reservation.invalid_request", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Post_MissingSeatNumber_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/reservations",
            new { eventId = SeedData.EventId, userId = Guid.NewGuid(), price = 1m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Post_MalformedJson_Returns400()
    {
        var response = await _client.PostAsync("/api/reservations",
            new StringContent("{ esto no es json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_BrokerDown_Returns503_WithoutLeakingInternals_NoPendingRequestLeft_AndSeatStaysAvailable()
    {
        var seat = await CreateAvailableSeatAsync();
        var before = await CountRequestsAsync();
        _factory.Publisher.ThrowOnPublish = new MessagePublishException("secreto-interno", new Exception("detalle-sdk"));

        var response = await _client.PostAsJsonAsync("/api/reservations", Body(seat));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secreto-interno", text);
        Assert.DoesNotContain("detalle-sdk", text);
        Assert.Equal(before, await CountRequestsAsync());

        // La compensación restauró el asiento: el catálogo lo sigue mostrando disponible y se puede reservar de nuevo.
        var catalog = await ReadAsync(await _client.GetAsync($"/api/seats?eventId={SeedData.EventId}"));
        var entry = catalog.EnumerateArray().Single(s => s.GetProperty("seatNumber").GetString() == seat);
        Assert.True(entry.GetProperty("isAvailable").GetBoolean());

        _factory.Publisher.ThrowOnPublish = null;
        var retry = await _client.PostAsJsonAsync("/api/reservations", Body(seat));
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
    }

    [Fact]
    public async Task Post_UnexpectedPublisherError_Returns500WithoutDetails()
    {
        var seat = await CreateAvailableSeatAsync();
        _factory.Publisher.ThrowOnPublish = new InvalidOperationException("detalle-inesperado");

        var response = await _client.PostAsJsonAsync("/api/reservations", Body(seat));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("detalle-inesperado", await response.Content.ReadAsStringAsync());
    }
}
