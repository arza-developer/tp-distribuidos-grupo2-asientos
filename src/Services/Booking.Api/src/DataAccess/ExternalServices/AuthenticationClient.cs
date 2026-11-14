using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Booking.Api.Business.Interfaces;

namespace Booking.Api.DataAccess.ExternalServices;

/// <summary>
/// Cliente de autenticacion para booking.
/// Nota: dado que se asegura de que no haya multiples consultas
/// intentando refrescar el token al mismo tiempo que puedan estresar a auth y hacer que 
/// caiga el servicio. Se basa en la premisa de que es auth interna entre servicios y que 
/// no todas las requests comparten el mismo token hacia payment.
/// </summary>
public sealed class AuthenticationClient : IAuthenticationClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthenticationClientConfiguration _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _token;
    private DateTimeOffset _refreshAfter;

    public AuthenticationClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthenticationClientConfiguration> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);

        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _refreshAfter)
                return _token;

            using var form = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret
                });

            var requestedAt = DateTimeOffset.UtcNow;
            using var client = _httpClientFactory.CreateClient("AuthServer");
            using var response = await client.PostAsync(
                _options.AuthenticationEndpoint, form, ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: ct);

            if (result is null ||
                string.IsNullOrWhiteSpace(result.AccessToken) ||
                result.ExpiresIn <= 0)
            {
                throw new InvalidOperationException(
                    "Auth Server returned an invalid token response.");
            }

            var refreshMargin = Math.Min(60, result.ExpiresIn / 2.0);

            _token = result.AccessToken;
            _refreshAfter = requestedAt.AddSeconds(
                result.ExpiresIn - refreshMargin);

            return _token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}