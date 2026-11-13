using Availability.Application.Specification;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Availability.Application.Abstractions.IRepository;
using Availability.Application.Bookings;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Availability.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Availability.Tests.Api;

/// <summary>
/// Identidad (TD-4): el Auth Server emite <c>sub = UserId</c>, así que Availability no le cree al body quién reserva
/// (<c>userId == sub</c>) y solo el dueño consulta su reserva.
/// </summary>
public class UserIdentityContractTests : IClassFixture<AvailabilityApiFactory>
{
    private static readonly Guid OtherUserId = Guid.Parse("9dd07a86-7777-8888-9999-4e185a88c1bb");

    private readonly AvailabilityApiFactory _factory;
    private readonly HttpClient _me;
    private readonly HttpClient _other;

    public UserIdentityContractTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.Publisher.Reset();
        _factory.Booking.Reset();
        _me = factory.CreateAuthenticatedClient(TestJwt.ForClient());
        _other = factory.CreateAuthenticatedClient(TestJwt.ForClient(OtherUserId));
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static object Body(string seat, Guid userId) =>
        new { eventId = SeedData.EventId, seatNumber = seat, userId, price = 15000m };

    private async Task<int> CountRequestsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ISeatRequestRepository>().ListAsync()).Count;
    }

    [Fact]
    public async Task Post_UserIdOfSomeoneElse_Returns403WithCode_AndTouchesNothing()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var before = await CountRequestsAsync();

        var response = await _me.PostAsJsonAsync("/api/reservations", Body(seat, OtherUserId)); // suplantación

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("reservation.user_mismatch", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Empty(_factory.Publisher.Published);
        Assert.Equal(before, await CountRequestsAsync());

        // el asiento sigue disponible: la suplantación no lo bloquea
        var catalog = await ReadAsync(await _me.GetAsync($"/api/seats?eventId={SeedData.EventId}"));
        var entry = catalog.EnumerateArray().Single(s => s.GetProperty("seatNumber").GetString() == seat);
        Assert.True(entry.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task Post_OwnUserId_Returns202_AndTheReservationBelongsToThatUser()
    {
        var seat = await _factory.CreateAvailableSeatAsync();

        var response = await _other.PostAsJsonAsync("/api/reservations", Body(seat, OtherUserId));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var id = (await ReadAsync(response)).GetProperty("reservationId").GetGuid();
        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ISeatRequestRepository>()
            .FirstOrDefaultAsync(SeatRequestSpecification.ByReservationId(id));
        Assert.Equal(OtherUserId, stored!.UserId);
    }

    [Fact]
    public async Task Get_ReservationOfAnotherUser_Returns404_AndDoesNotCallBooking()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var created = await _me.PostAsJsonAsync("/api/reservations", Body(seat, TestJwt.ClientUserId));
        var id = (await ReadAsync(created)).GetProperty("reservationId").GetGuid();
        _factory.Booking.Result = new BookingStatusInfo(ReservationStatus.Retained, DateTime.UtcNow.AddMinutes(5), seat);

        var response = await _other.GetAsync($"/api/reservations/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("reservation.not_found", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Empty(_factory.Booking.Calls);
    }

    [Fact]
    public async Task Get_OwnReservation_Returns200()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var created = await _me.PostAsJsonAsync("/api/reservations", Body(seat, TestJwt.ClientUserId));
        var id = (await ReadAsync(created)).GetProperty("reservationId").GetGuid();
        _factory.Booking.Result = new BookingStatusInfo(ReservationStatus.Retained, DateTime.UtcNow.AddMinutes(5), seat);

        var response = await _me.GetAsync($"/api/reservations/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Retained", (await ReadAsync(response)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_AnotherUsersAttemptDoesNotChangeTheOwnersReservation()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var created = await _me.PostAsJsonAsync("/api/reservations", Body(seat, TestJwt.ClientUserId));
        var id = (await ReadAsync(created)).GetProperty("reservationId").GetGuid();
        _factory.Booking.Result = new BookingStatusInfo(ReservationStatus.Rejected, null, seat);

        await _other.GetAsync($"/api/reservations/{id}"); // intento ajeno: 404 y sin sincronizar

        // el asiento sigue bloqueado para el dueño (no se liberó por la consulta ajena)
        var again = await _me.PostAsJsonAsync("/api/reservations", Body(seat, TestJwt.ClientUserId));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }
}
