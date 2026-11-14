using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Availability.Application.Abstractions.IRepository;
using Availability.Domain.Entities;
using Availability.Infrastructure.Persistence;
using Availability.Tests.Api;
using Availability.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Availability.Tests.Mvc;

/// <summary>Catálogo de las páginas (T5.2): público, con botón Reservar solo para usuarios con sesión.</summary>
public class CatalogTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly AvailabilityApiFactory _factory;

    public CatalogTests(AvailabilityApiFactory factory)
    {
        _factory = factory;
        _factory.LoginClient.Reset();
        _factory.Publisher.Reset();
        _factory.Booking.Reset();
    }

    private async Task<Seat> AddSeatAsync(Guid eventId, string number, decimal price = 9000m, bool available = true)
    {
        using var scope = _factory.Services.CreateScope();
        var seats = scope.ServiceProvider.GetRequiredService<ISeatRepository>();
        var seat = new Seat { SeatId = Guid.NewGuid(), EventId = eventId, SeatNumber = number, Price = price, IsAvailable = available };
        await seats.AddAsync(seat);
        await seats.SaveChangesAsync();
        return seat;
    }

    private static int CountReserveForms(string html) => Regex.Matches(html, "action=\"/Reservation/Reserve\"").Count;

    // ---------- anónimo ----------

    [Fact]
    public async Task Catalog_Anonymous_Returns200_WithoutRedirectingToLogin()
    {
        var response = await _factory.CreateBrowserClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Catalog_Anonymous_ListsTheSeededSeats_WithPricesAndStates()
    {
        var html = await (await _factory.CreateBrowserClient().GetAsync("/")).ReadHtmlAsync();

        Assert.Contains(SeedData.EventId.ToString(), html);
        foreach (var seat in new[] { "A-1", "A-2", "A-3", "B-1" }) Assert.Contains($"<td>{seat}</td>", html);
        Assert.Contains("$ 15.000", html);
        Assert.Contains("$ 18.000", html);
        Assert.Contains("$ 12.000", html);
        Assert.Contains("No disponible", html); // A-3 del seed
        Assert.Contains("Disponible", html);
    }

    [Fact]
    public async Task Catalog_Anonymous_HasNoReserveButtons_ButOffersLoginWithReturnUrl()
    {
        var html = await (await _factory.CreateBrowserClient().GetAsync("/")).ReadHtmlAsync();

        Assert.Equal(0, CountReserveForms(html));
        Assert.DoesNotContain("Reservar</button>", html);
        Assert.Contains("Ingresar para reservar", html);
        Assert.Contains("/Account/Login?returnUrl=%2F", html);
    }

    // ---------- con sesión ----------

    [Fact]
    public async Task Catalog_WithSession_ShowsReserveButtons_OnlyForAvailableSeats()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync();

        var html = await (await client.GetAsync("/")).ReadHtmlAsync();

        var available = Regex.Matches(html, "badge ok").Count;
        Assert.True(available >= 3, "el seed tiene A-1, A-2 y B-1 disponibles");
        Assert.Equal(available, CountReserveForms(html));
        Assert.DoesNotContain("Ingresar para reservar", html);
        Assert.Contains("cliente1", html); // el layout muestra al usuario
    }

    [Fact]
    public async Task Catalog_ReserveForm_CarriesEventSeatAndPrice_AndAnAntiforgeryToken()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync();

        var html = await (await client.GetAsync("/")).ReadHtmlAsync();
        var form = Regex.Match(html, "<form[^>]*action=\"/Reservation/Reserve\".*?</form>", RegexOptions.Singleline).Value;

        Assert.Contains("method=\"post\"", form);
        Assert.Contains($"name=\"eventId\" value=\"{SeedData.EventId}\"", form);
        Assert.Matches("name=\"seatNumber\" value=\"[A-Z]-\\d+\"", form);
        Assert.Matches("name=\"price\" value=\"\\d+(\\.\\d+)?\"", form);
        Assert.Contains("__RequestVerificationToken", form);
        Assert.DoesNotContain("userId", form); // el usuario sale de la sesión, nunca del formulario
    }

    [Fact]
    public async Task Catalog_ReserveButtons_AreNotShownToATokenOfAnotherRole_ItCannotEvenLogIn()
    {
        var client = _factory.CreateBrowserClient();
        await client.LoginAsync("servicio", "pw"); // rechazado: no hay sesión

        var html = await (await client.GetAsync("/")).ReadHtmlAsync();

        Assert.Equal(0, CountReserveForms(html));
        Assert.Contains("Ingresar para reservar", html);
    }

    [Fact]
    public async Task Catalog_AfterReservingThroughTheApi_ShowsTheSeatAsUnavailable()
    {
        var seat = await _factory.CreateAvailableSeatAsync();
        var before = await (await _factory.CreateBrowserClient().GetAsync("/")).ReadHtmlAsync();
        Assert.Matches($"<td>{seat}</td>\\s*<td>[^<]*</td>\\s*<td>\\s*<span class=\"badge ok\">", before);

        var api = _factory.CreateAuthenticatedClient();
        var reserved = await api.PostAsJsonAsync("/api/reservations",
            new { eventId = SeedData.EventId, seatNumber = seat, userId = TestJwt.ClientUserId, price = 15000m });
        Assert.Equal(HttpStatusCode.Accepted, reserved.StatusCode);

        var after = await (await _factory.CreateBrowserClient().GetAsync("/")).ReadHtmlAsync();
        Assert.Matches($"<td>{seat}</td>\\s*<td>[^<]*</td>\\s*<td>\\s*<span class=\"badge off\">", after);
    }

    // ---------- eventos ----------

    [Fact]
    public async Task Catalog_GroupsSeatsByEvent_AndFiltersWithTheQueryString()
    {
        var other = Guid.NewGuid();
        await AddSeatAsync(other, "Z-1");
        var client = _factory.CreateBrowserClient();

        var all = await (await client.GetAsync("/")).ReadHtmlAsync();
        var onlyOther = await (await client.GetAsync($"/?eventId={other}")).ReadHtmlAsync();
        var onlySeed = await (await client.GetAsync($"/?eventId={SeedData.EventId}")).ReadHtmlAsync();

        Assert.Contains($"<code>{other}</code>", all);
        Assert.Contains($"<code>{SeedData.EventId}</code>", all);
        Assert.Contains("<td>Z-1</td>", onlyOther);
        Assert.DoesNotContain("<td>A-1</td>", onlyOther);
        Assert.DoesNotContain("<td>Z-1</td>", onlySeed);
        Assert.Contains("<td>A-1</td>", onlySeed);
    }

    [Fact]
    public async Task Catalog_UnknownEvent_ShowsAnEmptyMessage()
    {
        var response = await _factory.CreateBrowserClient().GetAsync($"/?eventId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No hay asientos para mostrar.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task Catalog_MalformedEventId_DoesNotFail_ItShowsEverythingWithANotice()
    {
        var response = await _factory.CreateBrowserClient().GetAsync("/?eventId=esto-no-es-un-guid");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("El identificador de evento no es válido", html);
        Assert.Contains("<td>A-1</td>", html);
    }

    // ---------- seguridad ----------

    [Fact]
    public async Task Catalog_SeatNumbersWithMarkup_AreHtmlEncoded()
    {
        await AddSeatAsync(Guid.NewGuid(), "<i>x</i>");

        var raw = await _factory.CreateBrowserClient().GetStringAsync("/");

        Assert.DoesNotContain("<td><i>x</i></td>", raw);
        Assert.Contains("&lt;i&gt;x&lt;/i&gt;", raw);
    }

    // ---------- estáticos y auditoría ----------

    [Fact]
    public async Task Layout_LinksTheStylesheet_AndItIsServedAnonymously()
    {
        var client = _factory.CreateBrowserClient();

        var html = await client.GetStringAsync("/");
        var css = await client.GetAsync("/css/site.css");

        Assert.Matches("href=\"/css/site\\.css\\?v=[\\w-]+\"", html); // asp-append-version
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Equal("text/css", css.Content.Headers.ContentType!.MediaType);
        Assert.Contains(".badge", await css.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Catalog_LeavesInicioAndFin_ForTheUseCase_WithTheRequestCorrelationId()
    {
        var client = _factory.CreateBrowserClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "catalog-corr-1");

        await client.GetAsync("/");

        var audit = _factory.Logs.For("catalog-corr-1").Select(e => e.RenderMessage()).Where(m => m.StartsWith("INICIO:") || m.StartsWith("FIN:")).ToList();
        Assert.Equal(2, audit.Count);
        Assert.StartsWith("INICIO: GetSeatCatalogQuery", audit[0]);
        Assert.StartsWith("FIN: GetSeatCatalogQuery", audit[1]);
    }

    [Fact]
    public async Task PublicApiCatalog_IsUnchanged_ItStillHasNoEventId()
    {
        var json = await _factory.CreateClient().GetStringAsync("/api/seats");

        Assert.DoesNotContain("eventId", json); // contrato público intacto: SeatDto no se modificó
        Assert.Contains("seatNumber", json);
    }
}
