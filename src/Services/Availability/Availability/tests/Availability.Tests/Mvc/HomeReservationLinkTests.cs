using System.Net;
using Availability.Infrastructure.Persistence;
using Availability.Tests.Api;
using Availability.Tests.Support;

namespace Availability.Tests.Mvc;

/// <summary>Enlace «Ver estado» del inicio: aparece solo para los asientos donde el usuario autenticado tiene una reserva.</summary>
public class HomeReservationLinkTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;

    public HomeReservationLinkTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.LoginClient.Reset();
        _factory.Publisher.Reset();
        _factory.Booking.Reset();
    }

    private async Task<(HttpClient Client, Guid ReservationId)> ReserveAsync(string user, string password, string seat)
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync(user, password);
        var token = await client.GetAntiforgeryTokenAsync("/");
        var response = await client.PostFormAsync("/Reservation/Reserve", new Dictionary<string, string>
        {
            ["eventId"] = SeedData.EventId.ToString(), ["seatNumber"] = seat, ["price"] = "15000"
        }, token);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return (client, Guid.Parse(response.RedirectLocation()!["/Reservation/Status/".Length..]));
    }

    [Fact]
    public async Task OwnReservation_ShowsAnAnchorToItsStatus_OnHome()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var (client, id) = await ReserveAsync("cliente1", "cliente1-dev", seat);

        var html = await client.GetStringAsync("/");

        Assert.Contains($"href=\"/Reservation/Status/{id}\"", html);
        Assert.Contains("Ver estado", html);
    }

    [Fact]
    public async Task AnotherUsersReservation_DoesNotShowTheAnchor()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var (_, id) = await ReserveAsync("cliente1", "cliente1-dev", seat);
        var other = _factory.CreateBrowserClient();
        await other.LoginAsync("otro", "pw");

        var html = await other.GetStringAsync("/");

        Assert.DoesNotContain(id.ToString(), html);
    }

    [Fact]
    public async Task Anonymous_NeverSeesAnchors()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var (_, id) = await ReserveAsync("cliente1", "cliente1-dev", seat);

        var html = await _factory.CreateBrowserClient().GetStringAsync("/");

        Assert.DoesNotContain("Ver estado", html);
        Assert.DoesNotContain(id.ToString(), html);
    }

    [Fact]
    public async Task SeatsWithoutAReservationOfTheUser_HaveNoAnchor()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync("otro", "pw"); // este usuario no reserva nada en esta clase

        var html = await client.GetStringAsync("/");

        Assert.DoesNotContain("Ver estado", html);
        Assert.Contains(seat, html); // el asiento existe, pero sin reserva propia no hay enlace
    }
}
