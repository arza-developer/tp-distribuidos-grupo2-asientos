using System.Net;
using System.Text.RegularExpressions;
using Availability.Application.Abstractions.IRepository;
using Availability.Application.Bookings;
using Availability.Application.Exceptions;
using Availability.Application.Specification;
using Availability.Domain.Entities;
using Availability.Domain.Enums;
using Availability.Infrastructure.Persistence;
using Availability.Tests.Api;
using Availability.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using SeatsReservation.Contracts.Events;

namespace Availability.Tests.Mvc;

/// <summary>Reservar y ver el estado desde las páginas (T5.3).</summary>
public class ReservationPageTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;

    public ReservationPageTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.LoginClient.Reset();
        _factory.Publisher.Reset();
        _factory.Booking.Reset();
    }

    // ---------- soporte ----------

    private async Task<HttpClient> LoggedInAsync(string user = "cliente1", string password = "cliente1-dev")
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync(user, password);
        return client;
    }

    /// <summary>Token antiforgery tomado del catálogo (sus formularios de Reservar lo llevan).</summary>
    private static Task<string> TokenAsync(HttpClient client) => client.GetAntiforgeryTokenAsync("/");

    private static Dictionary<string, string> Form(string seat, Guid? eventId = null, string price = "15000") => new()
    {
        ["eventId"] = (eventId ?? SeedData.EventId).ToString(),
        ["seatNumber"] = seat,
        ["price"] = price
    };

    private async Task<HttpResponseMessage> ReserveAsync(HttpClient client, Dictionary<string, string> form) =>
        await client.PostFormAsync("/Reservation/Reserve", form, await TokenAsync(client));

    private async Task<SeatRequest?> StoredAsync(Guid reservationId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISeatRequestRepository>()
            .FirstOrDefaultAsync(SeatRequestSpecification.ByReservationId(reservationId));
    }

    private static Guid IdFrom(HttpResponseMessage redirect) =>
        Guid.Parse(redirect.RedirectLocation()!["/Reservation/Status/".Length..]);

    /// <summary>Reserva un asiento nuevo como cliente1 y devuelve (cliente, asiento, id de reserva).</summary>
    private async Task<(HttpClient Client, string Seat, Guid Id)> ReservedAsync(string user = "cliente1", string password = "cliente1-dev")
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = await LoggedInAsync(user, password);
        var response = await ReserveAsync(client, Form(seat));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return (client, seat, IdFrom(response));
    }

    // ---------- reservar ----------

    [Fact]
    public async Task Reserve_WithoutSession_RedirectsToLogin_AndReservesNothing()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var anonymous = _factory.CreateBrowserClient();
        var token = await anonymous.GetAntiforgeryTokenAsync("/Account/Login");

        var response = await anonymous.PostFormAsync("/Reservation/Reserve", Form(seat), token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.RedirectLocation()!);
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Reserve_Success_RedirectsToTheStatusPage_PersistsAsTheSessionUser_AndPublishesTheEvent()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = await LoggedInAsync();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "page-corr-1");

        var response = await ReserveAsync(client, Form(seat));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = IdFrom(response);
        var stored = await StoredAsync(id);
        Assert.Equal(TestJwt.ClientUserId, stored!.UserId);
        Assert.Equal(ReservationStatus.Pending, stored.Status);
        var evt = Assert.IsType<ReservationRequestedEvent>(Assert.Single(_factory.Publisher.Published).Message);
        Assert.Equal((id, TestJwt.ClientUserId, seat), (evt.ReservationId, evt.UserId, evt.SeatNumber));
        Assert.Equal("page-corr-1", evt.CorrelationId);
    }

    [Fact]
    public async Task Reserve_Success_ShowsAConfirmationMessage_OnTheStatusPage()
    {
        var (client, _, id) = await ReservedAsync();

        var html = await (await client.GetAsync($"/Reservation/Status/{id}")).ReadHtmlAsync();

        Assert.Contains("Solicitud recibida", html);
    }

    [Fact]
    public async Task Reserve_IgnoresAUserIdSentInTheForm_TheSessionDecidesWhoReserves()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = await LoggedInAsync();
        var form = Form(seat);
        form["userId"] = FakeAuthServerLoginClient.OtherUserId.ToString(); // intento de suplantación

        var id = IdFrom(await ReserveAsync(client, form));

        Assert.Equal(TestJwt.ClientUserId, (await StoredAsync(id))!.UserId);
        Assert.Equal(TestJwt.ClientUserId, ((ReservationRequestedEvent)_factory.Publisher.Published.Single().Message).UserId);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("15.000,5")]
    [InlineData("-5")]
    public async Task Reserve_PriceFromTheForm_IsOnlyInformative_TheCatalogPriceIsPublished(string price)
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = await LoggedInAsync();

        var response = await ReserveAsync(client, Form(seat, price: price));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(15000m, ((ReservationRequestedEvent)_factory.Publisher.Published.Single().Message).Price);
    }

    [Fact]
    public async Task Reserve_SameSeatTwice_SecondGoesBackToTheCatalogWithAMessage()
    {
        var (client, seat, _) = await ReservedAsync();

        var second = await ReserveAsync(client, Form(seat));
        var catalog = await (await client.GetAsync(second.RedirectLocation())).ReadHtmlAsync();

        Assert.Equal("/", second.RedirectLocation());
        Assert.Contains($"El asiento {seat} ya no está disponible.", catalog);
        Assert.Single(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Reserve_UnknownSeat_GoesBackToTheCatalogWithAMessage_AndPersistsNothing()
    {
        var client = await LoggedInAsync();

        var response = await ReserveAsync(client, Form("Z-99"));
        var catalog = await (await client.GetAsync(response.RedirectLocation())).ReadHtmlAsync();

        Assert.Equal("/", response.RedirectLocation());
        Assert.Contains("El asiento elegido no existe.", catalog);
        Assert.Empty(_factory.Publisher.Published);
    }

    [Theory]
    [InlineData("not-a-guid", "A-1")]
    [InlineData("", "A-1")]
    public async Task Reserve_MalformedEventId_GoesBackToTheCatalogWithAGenericMessage(string eventId, string seat)
    {
        var client = await LoggedInAsync();
        var form = Form(seat);
        form["eventId"] = eventId;

        var response = await ReserveAsync(client, form);
        var catalog = await (await client.GetAsync(response.RedirectLocation())).ReadHtmlAsync();

        Assert.Equal("/", response.RedirectLocation());
        Assert.Contains("Los datos de la reserva no son válidos.", catalog);
        Assert.Empty(_factory.Publisher.Published);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reserve_BlankSeatNumber_IsRejectedWithAGenericMessage(string seat)
    {
        var client = await LoggedInAsync();

        var response = await ReserveAsync(client, Form(seat));

        Assert.Equal("/", response.RedirectLocation());
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Reserve_WithoutAntiforgeryToken_Returns400_AndReservesNothing()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = await LoggedInAsync();

        var response = await client.PostFormAsync("/Reservation/Reserve", Form(seat), antiforgeryToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Reserve_ByGet_IsNotAnAction()
    {
        var client = await LoggedInAsync();

        var response = await client.GetAsync("/Reservation/Reserve");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Reserve_BrokerDown_Shows503Page_WithTrackingCode_NoInternals_AndTheSeatStaysAvailable()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = await LoggedInAsync();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "page-corr-503");
        _factory.Publisher.ThrowOnPublish = new MessagePublishException("secreto-interno", new Exception("detalle-sdk"));

        var response = await ReserveAsync(client, Form(seat));
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Servicio no disponible", html);
        Assert.Contains("page-corr-503", html); // código de seguimiento
        Assert.DoesNotContain("secreto-interno", html);
        Assert.DoesNotContain("detalle-sdk", html);

        _factory.Publisher.ThrowOnPublish = null;
        var catalog = await (await client.GetAsync("/")).ReadHtmlAsync();
        Assert.Matches($"<td>{seat}</td>\\s*<td>[^<]*</td>\\s*<td>\\s*<span class=\"badge ok\">", catalog); // compensación
        Assert.Equal(HttpStatusCode.Redirect, (await ReserveAsync(client, Form(seat))).StatusCode); // y se puede reintentar
    }

    [Fact]
    public async Task Reserve_AfterLogout_RedirectsToLoginAgain()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = await LoggedInAsync();
        var token = await TokenAsync(client);
        await client.PostFormAsync("/Account/Logout", [], token);

        var response = await client.PostFormAsync("/Reservation/Reserve", Form(seat), token);

        Assert.StartsWith("/Account/Login", response.RedirectLocation()!);
        Assert.Empty(_factory.Publisher.Published);
    }

    // ---------- estado ----------

    [Fact]
    public async Task Status_BookingDoesNotKnowIt_ShowsPending_WithTheContractFields()
    {
        var (client, seat, id) = await ReservedAsync();
        _factory.Booking.Result = null;

        var page = await client.GetAsync($"/Reservation/Status/{id}");
        var html = await page.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Pendiente de procesamiento", html);
        Assert.Contains("Su reserva aún no fue procesada", html);
        Assert.Contains(seat, html);
        Assert.Contains(id.ToString(), html);
        Assert.Contains("Actualizar", html);
        Assert.Contains("Volver al catálogo", html);
        Assert.DoesNotContain("Vence", html);
    }

    [Fact]
    public async Task Status_Retained_ShowsTheStatusAndTheExpiry()
    {
        var (client, _, id) = await ReservedAsync();
        var expires = new DateTime(2026, 11, 2, 15, 30, 45, DateTimeKind.Utc);
        _factory.Booking.Result = new BookingStatusInfo(ReservationStatus.Retained, expires, "x");

        var html = await (await client.GetAsync($"/Reservation/Status/{id}")).ReadHtmlAsync();

        Assert.Contains("Retenida", html);
        Assert.Contains("Se ha generado su reserva, esta pendiente de confirmación de pago", html);
        Assert.Contains("02/11/2026 15:30:45 UTC", html);
        Assert.Contains("badge warn", html);
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmed, "Confirmada", "badge ok")]
    [InlineData(ReservationStatus.Rejected, "Rechazada", "badge bad")]
    [InlineData(ReservationStatus.Expired, "Vencida", "badge bad")]
    public async Task Status_FinalStates_AreShownWithTheirLabelAndBadge(ReservationStatus status, string label, string badge)
    {
        var (client, _, id) = await ReservedAsync();
        _factory.Booking.Result = new BookingStatusInfo(status, null, "x");

        var html = await (await client.GetAsync($"/Reservation/Status/{id}")).ReadHtmlAsync();

        Assert.Contains(label, html);
        Assert.Contains(badge, html);
        Assert.DoesNotContain("Vence", html);
    }

    [Fact]
    public async Task Status_ManualRefresh_ShowsTheNewStateAfterBookingProcessesIt()
    {
        var (client, _, id) = await ReservedAsync();
        _factory.Booking.Result = null;
        Assert.Contains("Pendiente de procesamiento", await (await client.GetAsync($"/Reservation/Status/{id}")).ReadHtmlAsync());

        _factory.Booking.Result = new BookingStatusInfo(ReservationStatus.Confirmed, null, "x"); // Booking decidió
        var refreshed = await (await client.GetAsync($"/Reservation/Status/{id}")).ReadHtmlAsync();

        Assert.Contains("Confirmada", refreshed);
        Assert.Equal(ReservationStatus.Confirmed, (await StoredAsync(id))!.Status);
    }

    [Fact]
    public async Task Status_RejectedByBooking_ReleasesTheSeat_SoItCanBeReservedAgainFromThePage()
    {
        var (client, seat, id) = await ReservedAsync();
        _factory.Booking.Result = new BookingStatusInfo(ReservationStatus.Rejected, null, seat);

        await client.GetAsync($"/Reservation/Status/{id}"); // sincroniza (TD-1)
        var catalog = await (await client.GetAsync("/")).ReadHtmlAsync();
        var again = await ReserveAsync(client, Form(seat));

        Assert.Matches($"<td>{seat}</td>\\s*<td>[^<]*</td>\\s*<td>\\s*<span class=\"badge ok\">", catalog);
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.StartsWith("/Reservation/Status/", again.RedirectLocation()!);
    }

    [Fact]
    public async Task Status_Rejected_ShowsTheRejectionReasonReportedByBooking()
    {
        var (client, seat, id) = await ReservedAsync();
        _factory.Booking.Result = new BookingStatusInfo(ReservationStatus.Rejected, null, seat, "SeatOccupied");

        var html = await (await client.GetAsync($"/Reservation/Status/{id}")).ReadHtmlAsync();

        Assert.Contains("Motivo", html);
        Assert.Contains("SeatOccupied", html);
    }

    [Fact]
    public async Task Status_OfAnotherUsersReservation_Is404Page_AndBookingIsNeverCalled()
    {
        var (_, _, id) = await ReservedAsync();
        var other = await LoggedInAsync("otro", "pw");
        _factory.Booking.Reset();

        var response = await other.GetAsync($"/Reservation/Status/{id}");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No existe la reserva", html);
        Assert.Empty(_factory.Booking.Calls);
    }

    [Fact]
    public async Task Status_UnknownReservation_Is404FriendlyPage()
    {
        var client = await LoggedInAsync();

        var response = await client.GetAsync($"/Reservation/Status/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No existe la reserva", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task Status_MalformedId_Is404ByRouteConstraintOrMissingAction()
    {
        var client = await LoggedInAsync();

        var response = await client.GetAsync("/Reservation/Status/no-es-un-guid");

        Assert.Equal(HttpStatusCode.NotFound, (response).StatusCode);
    }

    [Fact]
    public async Task Status_WithoutSession_RedirectsToLoginWithReturnUrl()
    {
        var response = await _factory.CreateBrowserClient().GetAsync($"/Reservation/Status/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login?ReturnUrl=%2FReservation%2FStatus%2F", response.RedirectLocation()!);
        Assert.Empty(_factory.Booking.Calls);
    }

    [Fact]
    public async Task Status_BookingDown_Shows503Page_WithTrackingCode_NoInternals_AndChangesNothing()
    {
        var (client, _, id) = await ReservedAsync();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "page-corr-status-503");
        _factory.Booking.Throw = new BookingUnavailableException("secreto-interno", new HttpRequestException("detalle-red"));

        var response = await client.GetAsync($"/Reservation/Status/{id}");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("No pudimos consultar el estado", html);
        Assert.Contains("page-corr-status-503", html);
        Assert.Contains("no se perdió", html);
        Assert.DoesNotContain("secreto-interno", html);
        Assert.DoesNotContain("detalle-red", html);
        Assert.Equal(ReservationStatus.Pending, (await StoredAsync(id))!.Status);
    }

    // ---------- seguridad y auditoría ----------

    [Fact]
    public async Task Pages_EncodeSeatNumbersWithMarkup()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var seats = scope.ServiceProvider.GetRequiredService<ISeatRepository>();
            await seats.AddAsync(new Seat { SeatId = Guid.NewGuid(), EventId = SeedData.EventId, SeatNumber = "<i>x</i>", Price = 15000m, IsAvailable = true });
            await seats.SaveChangesAsync();
        }
        var client = await LoggedInAsync();

        var response = await ReserveAsync(client, Form("<i>x</i>"));
        var raw = await client.GetStringAsync(response.RedirectLocation());

        Assert.DoesNotContain("<td><i>x</i></td>", raw);
        Assert.Contains("&lt;i&gt;x&lt;/i&gt;", raw);
    }

    [Fact]
    public async Task ReserveAndStatus_LeaveInicioFinAuditEntries_WithTheRequestCorrelationId()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var client = await LoggedInAsync();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "page-audit-1");

        var response = await ReserveAsync(client, Form(seat));
        var statusClient = await LoggedInAsync();
        statusClient.DefaultRequestHeaders.Add("X-Correlation-ID", "page-audit-2");
        await statusClient.GetAsync(response.RedirectLocation());

        var reserve = _factory.Logs.For("page-audit-1").Select(e => e.RenderMessage()).Where(m => m.StartsWith("INICIO:") || m.StartsWith("FIN:")).ToList();
        var status = _factory.Logs.For("page-audit-2").Select(e => e.RenderMessage()).Where(m => m.StartsWith("INICIO:") || m.StartsWith("FIN:")).ToList();
        Assert.Contains(reserve, m => m.StartsWith("INICIO: RequestReservationCommand"));
        Assert.Contains(reserve, m => m.StartsWith("FIN: RequestReservationCommand"));
        Assert.Contains(status, m => m.StartsWith("INICIO: SyncReservationStatusCommand"));
        Assert.Contains(status, m => m.StartsWith("FIN: SyncReservationStatusCommand"));
    }

    [Fact]
    public async Task PageReservation_AndApiReservation_ShareTheSameGuard_TheApiSeesTheSeatAsTaken()
    {
        var (_, seat, _) = await ReservedAsync();
        var api = _factory.CreateAuthenticatedClient();

        var response = await api.PostAsync("/api/reservations", System.Net.Http.Json.JsonContent.Create(
            new { eventId = SeedData.EventId, seatNumber = seat, userId = TestJwt.ClientUserId, price = 15000m }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // mismo guarda IsAvailable para páginas y API
    }
}
