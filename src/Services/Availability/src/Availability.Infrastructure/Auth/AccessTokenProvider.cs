using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Availability.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Availability.Infrastructure.Auth;

/// <summary>
/// Obtiene el token M2M del Auth Server (<c>POST /connect/token</c>, <b>form-urlencoded</b>, grant <c>client_credentials</c>)
/// y lo cachea hasta <c>expires_in − TokenSkewSeconds</c>. Singleton: el cache se comparte entre requests y un
/// semáforo evita que varias requests simultáneas pidan el token a la vez.
/// <para>
/// Todo fallo (credenciales rechazadas, Auth Server caído, timeout, respuesta no interpretable, secreto sin configurar)
/// se informa como <see cref="BookingUnavailableException"/> (→ 503): sin token no se puede consultar a Booking.
/// El token nunca se escribe en los logs.
/// </para>
/// </summary>
public sealed class AccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AuthServerOptions> options,
    TimeProvider clock,
    ILogger<AccessTokenProvider> logger) : IAccessTokenProvider
{
    private sealed record CachedToken(string Value, DateTimeOffset ValidUntil);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile CachedToken? _cached;

    public async Task<string> GetTokenAsync(string correlationId, CancellationToken ct = default)
    {
        if (_cached is { } fast && fast.ValidUntil > clock.GetUtcNow()) return fast.Value;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is { } current && current.ValidUntil > clock.GetUtcNow()) return current.Value; // otra request ya lo renovó

            var (token, expiresIn) = await RequestTokenAsync(correlationId, ct);
            var lifetime = TimeSpan.FromSeconds(Math.Max(0, expiresIn - options.Value.TokenSkewSeconds));
            _cached = new CachedToken(token, clock.GetUtcNow() + lifetime);
            logger.LogInformation("Token M2M obtenido del Auth Server (cliente {ClientId}, vence en {ExpiresIn} s).",
                options.Value.ClientId, expiresIn);
            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate(string token)
    {
        _gate.Wait();
        try
        {
            if (_cached?.Value == token) _cached = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(string Token, int ExpiresIn)> RequestTokenAsync(string correlationId, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ClientSecret))
        {
            logger.LogError("AuthServer:ClientSecret no está configurado: no se puede pedir el token M2M para Booking.");
            throw new BookingUnavailableException("No se pudo obtener el token de acceso para Booking (credenciales M2M no configuradas).");
        }

        try
        {
            var http = httpClientFactory.CreateClient(AuthServerOptions.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = o.ClientId,
                    ["client_secret"] = o.ClientSecret
                })
            };
            request.Headers.Add("X-Correlation-ID", correlationId);

            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                logger.LogError(
                    "El Auth Server rechazó las credenciales M2M de {ClientId} (invalid_client): revisar AuthServer:ClientId y AuthServer:ClientSecret.",
                    o.ClientId);
                throw new BookingUnavailableException("No se pudo obtener el token de acceso para Booking (credenciales M2M rechazadas).");
            }

            if (!response.IsSuccessStatusCode)
                throw new BookingUnavailableException($"El Auth Server respondió {(int)response.StatusCode} al pedir el token M2M.");

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct);
            if (string.IsNullOrWhiteSpace(body?.AccessToken))
                throw new BookingUnavailableException("El Auth Server devolvió una respuesta de token no interpretable.");

            return (body.AccessToken, body.ExpiresIn);
        }
        catch (BookingUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // cancelación del llamador
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException)
        {
            logger.LogError(ex, "No se pudo contactar al Auth Server para obtener el token M2M.");
            throw new BookingUnavailableException("No se pudo obtener el token de acceso para Booking.", ex);
        }
    }
}
