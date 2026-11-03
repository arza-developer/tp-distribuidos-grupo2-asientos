using System.Net;
using System.Text;
using Availability.Application.Exceptions;
using Availability.Infrastructure.Auth;
using Availability.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Availability.Tests.Auth;

public class AccessTokenProviderTests
{
    internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            lock (Calls) Calls.Add((request, body));
            return await respond(request, ct);
        }
    }

    internal sealed class StubHttpClientFactory(HttpMessageHandler handler, Uri baseAddress, TimeSpan? timeout = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = baseAddress, Timeout = timeout ?? TimeSpan.FromSeconds(5) };
    }

    internal sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Entries) Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    private const string Secret = "availability-secret-dev";

    private readonly FakeTimeProvider _clock = new();
    private readonly ListLogger<AccessTokenProvider> _logger = new();
    private int _issued;

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private (AccessTokenProvider Provider, StubHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null,
        string secret = Secret, int expiresIn = 3600, TimeSpan? timeout = null)
    {
        var handler = new StubHandler(respond ?? ((_, _) =>
            Task.FromResult(Json(HttpStatusCode.OK,
                $$"""{"access_token":"token-{{Interlocked.Increment(ref _issued)}}","token_type":"Bearer","expires_in":{{expiresIn}}}"""))));
        var factory = new StubHttpClientFactory(handler, new Uri("http://auth.test/"), timeout);
        var options = Options.Create(new AuthServerOptions { ClientSecret = secret });
        return (new AccessTokenProvider(factory, options, _clock, _logger), handler);
    }

    // ---------- pedido al Auth Server ----------

    [Fact]
    public async Task GetToken_PostsFormEncodedClientCredentials_ToConnectToken()
    {
        var (provider, handler) = Create();

        var token = await provider.GetTokenAsync("corr-9");

        Assert.Equal("token-1", token);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://auth.test/connect/token", request.RequestUri!.ToString());
        Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("grant_type=client_credentials&client_id=availability-api&client_secret=availability-secret-dev", body);
        Assert.Equal("corr-9", Assert.Single(request.Headers.GetValues("X-Correlation-ID")));
    }

    [Fact]
    public async Task GetToken_SpecialCharactersInSecret_AreEncoded()
    {
        var (provider, handler) = Create(secret: "se&cre=to +1");

        await provider.GetTokenAsync("c");

        Assert.Equal("grant_type=client_credentials&client_id=availability-api&client_secret=se%26cre%3Dto+%2B1",
            handler.Calls.Single().Body);
    }

    // ---------- cache ----------

    [Fact]
    public async Task GetToken_IsCached_UntilNearExpiry()
    {
        var (provider, handler) = Create(expiresIn: 3600);

        var first = await provider.GetTokenAsync("c");
        _clock.Advance(TimeSpan.FromSeconds(3500)); // 3600 − 60 (skew) = 3540 s de validez en cache
        var second = await provider.GetTokenAsync("c");

        Assert.Equal(first, second);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task GetToken_IsRenewed_WhenWithinTheSkewOfExpiry()
    {
        var (provider, handler) = Create(expiresIn: 3600);

        var first = await provider.GetTokenAsync("c");
        _clock.Advance(TimeSpan.FromSeconds(3541)); // pasó exp − 60 s
        var second = await provider.GetTokenAsync("c");

        Assert.NotEqual(first, second);
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task GetToken_ShortLivedToken_IsNotCached()
    {
        var (provider, handler) = Create(expiresIn: 30); // menor que el skew

        await provider.GetTokenAsync("c");
        await provider.GetTokenAsync("c");

        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task GetToken_ConcurrentCallers_ShareASingleRequest()
    {
        var (provider, handler) = Create(async (_, ct) =>
        {
            await Task.Delay(100, ct);
            return Json(HttpStatusCode.OK, """{"access_token":"shared","expires_in":3600}""");
        });

        var tokens = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => provider.GetTokenAsync("c")));

        Assert.All(tokens, t => Assert.Equal("shared", t));
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Invalidate_DropsTheCachedToken_ForcingARefetch()
    {
        var (provider, handler) = Create();
        var first = await provider.GetTokenAsync("c");

        provider.Invalidate(first);
        var second = await provider.GetTokenAsync("c");

        Assert.NotEqual(first, second);
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task Invalidate_OfAnOldToken_DoesNotDropANewerOne()
    {
        var (provider, handler) = Create();
        var first = await provider.GetTokenAsync("c");
        provider.Invalidate(first);
        var second = await provider.GetTokenAsync("c");

        provider.Invalidate(first); // otra request llega tarde con el token viejo
        var third = await provider.GetTokenAsync("c");

        Assert.Equal(second, third);
        Assert.Equal(2, handler.Calls.Count);
    }

    // ---------- fallos ----------

    [Fact]
    public async Task GetToken_InvalidClient401_ThrowsBookingUnavailable_AndLogsAnActionableError()
    {
        var (provider, _) = Create((_, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""")));

        await Assert.ThrowsAsync<BookingUnavailableException>(() => provider.GetTokenAsync("c"));

        var error = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("invalid_client", error.Message);
        Assert.Contains("AuthServer:ClientSecret", error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetToken_OtherErrorStatus_ThrowsBookingUnavailable(HttpStatusCode status)
    {
        var (provider, _) = Create((_, _) => Task.FromResult(Json(status, "{}")));

        await Assert.ThrowsAsync<BookingUnavailableException>(() => provider.GetTokenAsync("c"));
    }

    [Fact]
    public async Task GetToken_NetworkFailure_ThrowsBookingUnavailable_KeepingInnerException()
    {
        var inner = new HttpRequestException("connection refused");
        var (provider, _) = Create((_, _) => throw inner);

        var ex = await Assert.ThrowsAsync<BookingUnavailableException>(() => provider.GetTokenAsync("c"));

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public async Task GetToken_Timeout_ThrowsBookingUnavailable()
    {
        var (provider, _) = Create(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(HttpStatusCode.OK, "{}");
        }, timeout: TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAsync<BookingUnavailableException>(() => provider.GetTokenAsync("c"));
    }

    [Fact]
    public async Task GetToken_CallerCancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (provider, _) = Create(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(HttpStatusCode.OK, "{}");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetTokenAsync("c", cts.Token));
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("""{"token_type":"Bearer","expires_in":3600}""")]
    [InlineData("""{"access_token":"","expires_in":3600}""")]
    [InlineData("null")]
    public async Task GetToken_UnrecognizableBody_ThrowsBookingUnavailable(string body)
    {
        var (provider, _) = Create((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body)));

        await Assert.ThrowsAsync<BookingUnavailableException>(() => provider.GetTokenAsync("c"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetToken_WithoutConfiguredSecret_FailsWithoutCallingTheAuthServer(string secret)
    {
        var (provider, handler) = Create(secret: secret);

        await Assert.ThrowsAsync<BookingUnavailableException>(() => provider.GetTokenAsync("c"));

        Assert.Empty(handler.Calls);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("ClientSecret"));
    }

    [Fact]
    public async Task GetToken_AfterAFailure_RetriesInsteadOfCachingTheFailure()
    {
        var calls = 0;
        var (provider, handler) = Create((_, _) =>
            Task.FromResult(++calls == 1
                ? Json(HttpStatusCode.ServiceUnavailable, "{}")
                : Json(HttpStatusCode.OK, """{"access_token":"recovered","expires_in":3600}""")));

        await Assert.ThrowsAsync<BookingUnavailableException>(() => provider.GetTokenAsync("c"));
        var token = await provider.GetTokenAsync("c");

        Assert.Equal("recovered", token);
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task Logs_NeverContainTheTokenOrTheSecret()
    {
        var (provider, _) = Create();

        var token = await provider.GetTokenAsync("c");

        Assert.NotEmpty(_logger.Entries);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains(token) || e.Message.Contains(Secret));
    }
}
