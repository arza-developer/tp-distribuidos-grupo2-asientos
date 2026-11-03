using System.Net;
using System.Text;
using Availability.Application.Abstractions;
using Availability.Application.Exceptions;
using Availability.Domain.Enums;
using Availability.Infrastructure;
using Availability.Infrastructure.Auth;
using Availability.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Availability.Tests.Auth;

/// <summary>
/// Cadena real de DI: <c>IBookingClient</c> → <c>BookingAuthHandler</c> → <c>AccessTokenProvider</c> → Auth Server (falso)
/// y luego Booking (falso) con el Bearer. Equivale a lo que ocurre en producción, sin red.
/// </summary>
public class MachineToMachineChainTests
{
    private sealed class FakeCorrelation : ICorrelationIdAccessor
    {
        public string CorrelationId => "corr-chain";
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static ServiceProvider Build(
        AccessTokenProviderTests.StubHandler authServer, AccessTokenProviderTests.StubHandler booking,
        string? secret = "availability-secret-dev", FakeTimeProvider? clock = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Booking:BaseUrl"] = "http://booking.test:5102",
            ["AuthServer:BaseUrl"] = "http://auth.test:5104",
            ["AuthServer:ClientSecret"] = secret
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddSingleton<ICorrelationIdAccessor>(new FakeCorrelation());
        if (clock is not null) services.AddSingleton<TimeProvider>(clock);
        services.AddInfrastructure();

        // Se reemplazan solo los manejadores primarios (la red); los DelegatingHandlers reales quedan en la cadena.
        services.AddHttpClient(AuthServerOptions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => authServer);
        services.AddHttpClient<IBookingClient, Availability.Infrastructure.Bookings.BookingClient>()
            .ConfigurePrimaryHttpMessageHandler(() => booking);
        return services.BuildServiceProvider();
    }

    private static AccessTokenProviderTests.StubHandler AuthServerIssuing(Func<int, string> token, int expiresIn = 3600)
    {
        var n = 0;
        return new AccessTokenProviderTests.StubHandler((_, _) =>
            Task.FromResult(Json(HttpStatusCode.OK, $$"""{"access_token":"{{token(++n)}}","token_type":"Bearer","expires_in":{{expiresIn}}}""")));
    }

    private static AccessTokenProviderTests.StubHandler BookingAnswering(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    {
        var n = 0;
        return new AccessTokenProviderTests.StubHandler((request, _) => Task.FromResult(respond(request, ++n)));
    }

    private const string RetainedBody = """{"seatNumber":"A-1","status":"Retenida"}""";

    [Fact]
    public async Task BookingCall_CarriesTheM2MBearerTokenObtainedFromTheAuthServer()
    {
        var auth = AuthServerIssuing(n => $"m2m-{n}");
        var booking = BookingAnswering((_, _) => Json(HttpStatusCode.OK, RetainedBody));
        await using var provider = Build(auth, booking);
        using var scope = provider.CreateScope();

        var info = await scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid());

        Assert.Equal(ReservationStatus.Retained, info!.Status);
        var tokenRequest = Assert.Single(auth.Calls);
        Assert.Equal("http://auth.test:5104/connect/token", tokenRequest.Request.RequestUri!.ToString());
        Assert.Contains("grant_type=client_credentials", tokenRequest.Body);
        var bookingRequest = Assert.Single(booking.Calls).Request;
        Assert.Equal("Bearer", bookingRequest.Headers.Authorization!.Scheme);
        Assert.Equal("m2m-1", bookingRequest.Headers.Authorization.Parameter);
        Assert.Equal("corr-chain", Assert.Single(bookingRequest.Headers.GetValues("X-Correlation-ID")));
    }

    [Fact]
    public async Task ManyBookingCalls_ReuseTheSameCachedToken()
    {
        var auth = AuthServerIssuing(n => $"m2m-{n}");
        var booking = BookingAnswering((_, _) => Json(HttpStatusCode.OK, RetainedBody));
        await using var provider = Build(auth, booking);

        for (var i = 0; i < 5; i++)
        {
            using var scope = provider.CreateScope(); // una "request" distinta cada vez
            await scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid());
        }

        Assert.Single(auth.Calls); // el cache del token sobrevive entre requests (proveedor singleton)
        Assert.All(booking.Calls, c => Assert.Equal("m2m-1", c.Request.Headers.Authorization!.Parameter));
        Assert.Equal(5, booking.Calls.Count);
    }

    [Fact]
    public async Task Booking401_RenewsTheToken_AndTheCallSucceedsOnRetry()
    {
        var auth = AuthServerIssuing(n => $"m2m-{n}");
        var booking = BookingAnswering((_, call) =>
            call == 1 ? Json(HttpStatusCode.Unauthorized, "{}") : Json(HttpStatusCode.OK, RetainedBody));
        await using var provider = Build(auth, booking);
        using var scope = provider.CreateScope();

        var info = await scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid());

        Assert.Equal(ReservationStatus.Retained, info!.Status);
        Assert.Equal(2, auth.Calls.Count);
        Assert.Equal("m2m-1", booking.Calls[0].Request.Headers.Authorization!.Parameter);
        Assert.Equal("m2m-2", booking.Calls[1].Request.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task Booking401Persistently_EndsAsBookingUnavailable()
    {
        var auth = AuthServerIssuing(n => $"m2m-{n}");
        var booking = BookingAnswering((_, _) => Json(HttpStatusCode.Unauthorized, "{}"));
        await using var provider = Build(auth, booking);
        using var scope = provider.CreateScope();

        await Assert.ThrowsAsync<BookingUnavailableException>(
            () => scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid()));

        Assert.Equal(2, booking.Calls.Count); // un intento + un único reintento
    }

    [Fact]
    public async Task AuthServerDown_MeansBookingIsNeverCalled_AndTheResultIsBookingUnavailable()
    {
        var auth = new AccessTokenProviderTests.StubHandler((_, _) => throw new HttpRequestException("connection refused"));
        var booking = BookingAnswering((_, _) => Json(HttpStatusCode.OK, RetainedBody));
        await using var provider = Build(auth, booking);
        using var scope = provider.CreateScope();

        await Assert.ThrowsAsync<BookingUnavailableException>(
            () => scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid()));

        Assert.Empty(booking.Calls);
    }

    [Fact]
    public async Task WrongClientSecret_AuthServerRejects_AndBookingIsNeverCalled()
    {
        var auth = new AccessTokenProviderTests.StubHandler((_, _) =>
            Task.FromResult(Json(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""")));
        var booking = BookingAnswering((_, _) => Json(HttpStatusCode.OK, RetainedBody));
        await using var provider = Build(auth, booking, secret: "equivocado");
        using var scope = provider.CreateScope();

        await Assert.ThrowsAsync<BookingUnavailableException>(
            () => scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid()));

        Assert.Contains("client_secret=equivocado", Assert.Single(auth.Calls).Body);
        Assert.Empty(booking.Calls);
    }

    [Fact]
    public async Task MissingClientSecret_FailsFastWithoutTouchingTheNetwork()
    {
        var auth = AuthServerIssuing(n => $"m2m-{n}");
        var booking = BookingAnswering((_, _) => Json(HttpStatusCode.OK, RetainedBody));
        await using var provider = Build(auth, booking, secret: null);
        using var scope = provider.CreateScope();

        await Assert.ThrowsAsync<BookingUnavailableException>(
            () => scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid()));

        Assert.Empty(auth.Calls);
        Assert.Empty(booking.Calls);
    }

    [Fact]
    public async Task TokenExpiry_TriggersARenewalOnTheNextCall()
    {
        var clock = new FakeTimeProvider();
        var auth = AuthServerIssuing(n => $"m2m-{n}");
        var booking = BookingAnswering((_, _) => Json(HttpStatusCode.OK, RetainedBody));
        await using var provider = Build(auth, booking, clock: clock);

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid());
        clock.Advance(TimeSpan.FromHours(1)); // vence (3600 s − 60 s de margen)
        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IBookingClient>().GetStatusAsync(Guid.NewGuid());

        Assert.Equal(2, auth.Calls.Count);
        Assert.Equal("m2m-2", booking.Calls[1].Request.Headers.Authorization!.Parameter);
    }
}
