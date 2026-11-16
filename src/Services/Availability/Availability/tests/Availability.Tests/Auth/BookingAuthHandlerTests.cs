using System.Net;
using System.Text;
using Availability.Application.Abstractions;
using Availability.Application.Exceptions;
using Availability.Infrastructure.Auth;
using Availability.Tests.Auth;
using Microsoft.Extensions.Logging.Abstractions;

namespace Availability.Tests.Auth;

public class BookingAuthHandlerTests
{
    private sealed class FakeTokenProvider : IAccessTokenProvider
    {
        private int _n;
        private string? _current;
        public List<string> Invalidated { get; } = [];
        public Exception? ThrowOnGet { get; set; }
        public string? LastCorrelationId { get; private set; }

        public Task<string> GetTokenAsync(string correlationId, CancellationToken ct = default)
        {
            LastCorrelationId = correlationId;
            if (ThrowOnGet is not null) throw ThrowOnGet;
            return Task.FromResult(_current ??= $"token-{++_n}");
        }

        public void Invalidate(string token)
        {
            Invalidated.Add(token);
            if (_current == token) _current = null;
        }
    }

    private sealed class FakeCorrelation : ICorrelationIdAccessor
    {
        public string CorrelationId => "corr-5";
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code) { Content = new StringContent("{}", Encoding.UTF8) };

    private static (HttpClient Client, AccessTokenProviderTests.StubHandler Inner, FakeTokenProvider Tokens) Create(
        Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    {
        var calls = 0;
        var inner = new AccessTokenProviderTests.StubHandler((request, _) => Task.FromResult(respond(request, ++calls)));
        var tokens = new FakeTokenProvider();
        var handler = new BookingAuthHandler(tokens, new FakeCorrelation(), NullLogger<BookingAuthHandler>.Instance)
        {
            InnerHandler = inner
        };
        return (new HttpClient(handler) { BaseAddress = new Uri("http://booking.test/") }, inner, tokens);
    }

    [Fact]
    public async Task SendsTheBearerToken_AndPassesTheCorrelationIdToTheProvider()
    {
        var (client, inner, tokens) = Create((_, _) => Status(HttpStatusCode.OK));

        var response = await client.GetAsync("api/bookings/x");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (request, _) = Assert.Single(inner.Calls);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token-1", request.Headers.Authorization.Parameter);
        Assert.Equal("corr-5", tokens.LastCorrelationId);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task NonUnauthorizedResponses_PassThroughWithoutRetry(HttpStatusCode code)
    {
        var (client, inner, tokens) = Create((_, _) => Status(code));

        var response = await client.GetAsync("api/bookings/x");

        Assert.Equal(code, response.StatusCode);
        Assert.Single(inner.Calls);
        Assert.Empty(tokens.Invalidated);
    }

    [Fact]
    public async Task Booking401_InvalidatesTheToken_FetchesANewOne_AndRetriesOnce()
    {
        var (client, inner, tokens) = Create((request, call) =>
            call == 1 ? Status(HttpStatusCode.Unauthorized) : Status(HttpStatusCode.OK));

        var response = await client.GetAsync("api/bookings/x");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls.Count);
        Assert.Equal(["token-1"], tokens.Invalidated);
        Assert.Equal("token-1", inner.Calls[0].Request.Headers.Authorization!.Parameter);
        Assert.Equal("token-2", inner.Calls[1].Request.Headers.Authorization!.Parameter);
        Assert.Equal(inner.Calls[0].Request.RequestUri, inner.Calls[1].Request.RequestUri);
    }

    [Fact]
    public async Task Booking401Twice_ReturnsThe401_WithoutLooping()
    {
        var (client, inner, _) = Create((_, _) => Status(HttpStatusCode.Unauthorized));

        var response = await client.GetAsync("api/bookings/x");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, inner.Calls.Count); // un intento + un solo reintento
    }

    [Fact]
    public async Task Retry_KeepsTheOtherRequestHeaders_ButNotTheOldAuthorization()
    {
        var (client, inner, _) = Create((_, call) => call == 1 ? Status(HttpStatusCode.Unauthorized) : Status(HttpStatusCode.OK));
        var request = new HttpRequestMessage(HttpMethod.Get, "api/bookings/x");
        request.Headers.Add("X-Correlation-ID", "corr-5");

        await client.SendAsync(request);

        var retry = inner.Calls[1].Request;
        Assert.Equal("corr-5", Assert.Single(retry.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("token-2", retry.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task Booking401_OnARequestWithBody_IsNotRetried()
    {
        var (client, inner, tokens) = Create((_, _) => Status(HttpStatusCode.Unauthorized));

        var response = await client.PostAsync("api/bookings", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(inner.Calls); // el cuerpo ya se consumió: no se puede reenviar de forma segura
        Assert.Empty(tokens.Invalidated);
    }

    [Fact]
    public async Task TokenProviderFailure_PropagatesAsBookingUnavailable_WithoutCallingBooking()
    {
        var (client, inner, tokens) = Create((_, _) => Status(HttpStatusCode.OK));
        tokens.ThrowOnGet = new BookingUnavailableException("Auth Server caído");

        await Assert.ThrowsAsync<BookingUnavailableException>(() => client.GetAsync("api/bookings/x"));

        Assert.Empty(inner.Calls);
    }
}
