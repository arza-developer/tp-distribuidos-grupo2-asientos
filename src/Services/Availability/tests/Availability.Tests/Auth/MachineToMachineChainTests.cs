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
/// (TD-11/T8.6: se conserva un solo caso de la cadena; el resto de las reglas M2M las cubren AccessTokenProviderTests y BookingAuthHandlerTests.)
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

    private const string RetainedBody = """{"seatNumber":"A-1","status":"Retained"}""";

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
}
