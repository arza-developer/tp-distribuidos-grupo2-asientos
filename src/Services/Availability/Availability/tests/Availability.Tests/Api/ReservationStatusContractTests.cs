using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Availability.Application.Abstractions.IRepository;
using Availability.Application.Bookings;
using Availability.Application.Exceptions;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Availability.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Availability.Tests.Api;

/// <summary>Contrato de GET /api/reservations/{id} (paso 7 del TP): Availability consulta a Booking.</summary>
public class ReservationStatusContractTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;
    private readonly HttpClient _client;

    public ReservationStatusContractTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.Publisher.Reset();
        _factory.Booking.Reset();
        _client = factory.CreateAuthenticatedClient();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Crea una reserva real vía POST (asiento propio) y devuelve su id y número de asiento.</summary>
    private async Task<(Guid Id, string Seat)> CreateReservationAsync()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var response = await _client.PostAsJsonAsync("/api/reservations",
            new { eventId = SeedData.EventId, seatNumber = seat, userId = TestJwt.ClientUserId, price = 15000m });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("reservationId").GetGuid(), seat);
    }

    [Fact]
    public async Task Get_BookingDoesNotKnowItYet_Returns200Pending_WithContractKeys()
    {
        var (id, seat) = await CreateReservationAsync();
        _factory.Booking.Result = null;

        var response = await _client.GetAsync($"/api/reservations/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadAsync(response);
        Assert.Equal(["reservationId", "status", "message", "seatNumber", "expiresAtUtc"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(id, json.GetProperty("reservationId").GetGuid());
        Assert.Equal("Pending", json.GetProperty("status").GetString());
        Assert.Equal("La reserva aún no fue procesada por Booking", json.GetProperty("message").GetString());
        Assert.Equal(seat, json.GetProperty("seatNumber").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("expiresAtUtc").ValueKind);
        Assert.Equal([id], _factory.Booking.Calls);
    }

    [Fact]
    public async Task Get_Retained_ReturnsStatusAndExpiry()
    {
        var (id, _) = await CreateReservationAsync();
        var expires = DateTime.UtcNow.AddMinutes(5);
        _factory.Booking.Result = new BookingStatusInfo(ReservationStatus.Retained, expires, "x");

        var json = await ReadAsync(await _client.GetAsync($"/api/reservations/{id}"));

        Assert.Equal("Retained", json.GetProperty("status").GetString());
        Assert.Equal("Reserva retenida, pendiente de pago", json.GetProperty("message").GetString());
        Assert.Equal(expires, json.GetProperty("expiresAtUtc").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmed, "Confirmed", "Reserva confirmada")]
    [InlineData(ReservationStatus.Rejected, "Rejected", "Reserva rechazada")]
    [InlineData(ReservationStatus.Expired, "Expired", "La reserva venció")]
    public async Task Get_FinalStatuses_AreReportedWithTheirMessage(ReservationStatus status, string text, string message)
    {
        var (id, _) = await CreateReservationAsync();
        _factory.Booking.Result = new BookingStatusInfo(status, null, "x");

        var json = await ReadAsync(await _client.GetAsync($"/api/reservations/{id}"));

        Assert.Equal(text, json.GetProperty("status").GetString());
        Assert.Equal(message, json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Get_UnknownReservation_Returns404WithCode_AndDoesNotCallBooking()
    {
        var response = await _client.GetAsync($"/api/reservations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("reservation.not_found", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Empty(_factory.Booking.Calls);
    }

    [Fact]
    public async Task Get_MalformedId_Returns404_ByRouteConstraint()
    {
        var response = await _client.GetAsync("/api/reservations/no-es-un-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_factory.Booking.Calls);
    }

    [Fact]
    public async Task Get_BookingDown_Returns503_WithoutLeakingInternals()
    {
        var (id, _) = await CreateReservationAsync();
        _factory.Booking.Throw = new BookingUnavailableException("secreto-interno", new HttpRequestException("detalle-red"));

        var response = await _client.GetAsync($"/api/reservations/{id}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secreto-interno", text);
        Assert.DoesNotContain("detalle-red", text);
    }

    // ---------- TD-1: el GET sincroniza el estado local y libera el asiento ----------

    private Task<HttpResponseMessage> PostAgainAsync(string seat) =>
        _client.PostAsJsonAsync("/api/reservations",
            new { eventId = SeedData.EventId, seatNumber = seat, userId = TestJwt.ClientUserId, price = 15000m });

    private async Task<ReservationStatus> LocalStatusAsync(Guid reservationId)
    {
        using var scope = _factory.Services.CreateScope();
        var request = await scope.ServiceProvider.GetRequiredService<ISeatRequestRepository>()
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId);
        return request!.Status;
    }

    [Theory]
    [InlineData(ReservationStatus.Rejected)]
    [InlineData(ReservationStatus.Expired)]
    public async Task Get_RejectedOrExpiredByBooking_SyncsLocalStatus_AndReleasesSeat(ReservationStatus bookingStatus)
    {
        var (id, seat) = await CreateReservationAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await PostAgainAsync(seat)).StatusCode); // bloqueado mientras está Pending
        _factory.Booking.Result = new BookingStatusInfo(bookingStatus, null, seat);

        await _client.GetAsync($"/api/reservations/{id}");

        Assert.Equal(bookingStatus, await LocalStatusAsync(id));
        Assert.Equal(HttpStatusCode.Accepted, (await PostAgainAsync(seat)).StatusCode); // liberado
    }

    [Theory]
    [InlineData(ReservationStatus.Retained)]
    [InlineData(ReservationStatus.Confirmed)]
    public async Task Get_RetainedOrConfirmedByBooking_SyncsLocalStatus_AndKeepsSeatBlocked(ReservationStatus bookingStatus)
    {
        var (id, seat) = await CreateReservationAsync();
        _factory.Booking.Result = new BookingStatusInfo(bookingStatus, DateTime.UtcNow.AddMinutes(5), seat);

        await _client.GetAsync($"/api/reservations/{id}");

        Assert.Equal(bookingStatus, await LocalStatusAsync(id));
        Assert.Equal(HttpStatusCode.Conflict, (await PostAgainAsync(seat)).StatusCode);
    }

    [Fact]
    public async Task Get_OrphanReservation_BookingNeverKnewIt_ExpiresAfterTimeout_AndReleasesSeat()
    {
        var (id, seat) = await CreateReservationAsync();
        _factory.Booking.Result = null;

        var young = await ReadAsync(await _client.GetAsync($"/api/reservations/{id}"));
        Assert.Equal("Pending", young.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await PostAgainAsync(seat)).StatusCode);

        _factory.Clock.Advance(TimeSpan.FromMinutes(11)); // supera ReservationSync:PendingTimeoutMinutes (10)
        var old = await ReadAsync(await _client.GetAsync($"/api/reservations/{id}"));

        Assert.Equal("Expired", old.GetProperty("status").GetString());
        Assert.Equal("La reserva venció: Booking no llegó a procesarla", old.GetProperty("message").GetString());
        Assert.Equal(ReservationStatus.Expired, await LocalStatusAsync(id));
        Assert.Equal(HttpStatusCode.Accepted, (await PostAgainAsync(seat)).StatusCode);
    }

    [Fact]
    public async Task Get_BookingDown_ChangesNothing_EvenIfTheReservationLooksOrphan()
    {
        var (id, seat) = await CreateReservationAsync();
        _factory.Clock.Advance(TimeSpan.FromMinutes(60));
        _factory.Booking.Throw = new BookingUnavailableException("caído");

        var response = await _client.GetAsync($"/api/reservations/{id}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(ReservationStatus.Pending, await LocalStatusAsync(id));
        Assert.Equal(HttpStatusCode.Conflict, (await PostAgainAsync(seat)).StatusCode);
    }
}
