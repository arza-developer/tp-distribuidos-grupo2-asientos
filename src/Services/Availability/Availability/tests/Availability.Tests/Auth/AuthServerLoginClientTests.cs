using System.Net;
using System.Text;
using Availability.Application.Exceptions;
using Availability.Infrastructure.Auth;
using Microsoft.Extensions.Logging;

namespace Availability.Tests.Auth;

public class AuthServerLoginClientTests
{
    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private readonly AccessTokenProviderTests.ListLogger<AuthServerLoginClient> _logger = new();

    private (AuthServerLoginClient Client, AccessTokenProviderTests.StubHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
    {
        var handler = new AccessTokenProviderTests.StubHandler(respond);
        var factory = new AccessTokenProviderTests.StubHttpClientFactory(handler, new Uri("http://auth.test/"), timeout);
        return (new AuthServerLoginClient(factory, _logger), handler);
    }

    private (AuthServerLoginClient Client, AccessTokenProviderTests.StubHandler Handler) CreateReturning(HttpStatusCode status, string json) =>
        Create((_, _) => Task.FromResult(Json(status, json)));

    [Fact]
    public async Task Login_Success_ReturnsTheAccessToken_PostingFormEncodedPasswordGrant()
    {
        var (client, handler) = CreateReturning(HttpStatusCode.OK, """{"access_token":"el-token","token_type":"Bearer","expires_in":3600}""");

        var token = await client.LoginAsync("cliente1", "cliente1-dev", "corr-1");

        Assert.Equal("el-token", token);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://auth.test/connect/token", request.RequestUri!.ToString());
        Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("grant_type=password&username=cliente1&password=cliente1-dev", body);
        Assert.Equal("corr-1", Assert.Single(request.Headers.GetValues("X-Correlation-ID")));
    }

    [Fact]
    public async Task Login_SpecialCharacters_AreEncoded()
    {
        var (client, handler) = CreateReturning(HttpStatusCode.OK, """{"access_token":"t"}""");

        await client.LoginAsync("a&b=c", "p ás&=+%", "c");

        Assert.Equal("grant_type=password&username=a%26b%3Dc&password=p+%C3%A1s%26%3D%2B%25", handler.Calls.Single().Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Login_RejectedCredentials_ReturnNull_NotAnException(HttpStatusCode status)
    {
        var (client, _) = CreateReturning(status, """{"error":"invalid_grant"}""");

        Assert.Null(await client.LoginAsync("cliente1", "mala", "c"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Login_AuthServerErrors_ThrowAuthServerUnavailable(HttpStatusCode status)
    {
        var (client, _) = CreateReturning(status, "{}");

        await Assert.ThrowsAsync<AuthServerUnavailableException>(() => client.LoginAsync("u", "p", "c"));
    }

    [Fact]
    public async Task Login_NetworkFailure_ThrowsAuthServerUnavailable_KeepingInnerException()
    {
        var inner = new HttpRequestException("connection refused");
        var (client, _) = Create((_, _) => throw inner);

        var ex = await Assert.ThrowsAsync<AuthServerUnavailableException>(() => client.LoginAsync("u", "p", "c"));

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public async Task Login_Timeout_ThrowsAuthServerUnavailable()
    {
        var (client, _) = Create(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(HttpStatusCode.OK, "{}");
        }, timeout: TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAsync<AuthServerUnavailableException>(() => client.LoginAsync("u", "p", "c"));
    }

    [Fact]
    public async Task Login_CallerCancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (client, _) = Create(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Json(HttpStatusCode.OK, "{}");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LoginAsync("u", "p", "c", cts.Token));
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("""{"token_type":"Bearer"}""")]
    [InlineData("""{"access_token":""}""")]
    [InlineData("null")]
    public async Task Login_UnrecognizableBody_ThrowsAuthServerUnavailable(string body)
    {
        var (client, _) = CreateReturning(HttpStatusCode.OK, body);

        await Assert.ThrowsAsync<AuthServerUnavailableException>(() => client.LoginAsync("u", "p", "c"));
    }

    [Fact]
    public async Task Logs_NeverContainThePasswordOrTheToken()
    {
        var (client, _) = CreateReturning(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        await client.LoginAsync("cliente1", "clave-super-secreta", "c");

        var (okClient, _) = CreateReturning(HttpStatusCode.OK, """{"access_token":"token-secreto"}""");
        await okClient.LoginAsync("cliente1", "clave-super-secreta", "c");

        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("clave-super-secreta") || e.Message.Contains("token-secreto"));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("cliente1"));
    }
}
