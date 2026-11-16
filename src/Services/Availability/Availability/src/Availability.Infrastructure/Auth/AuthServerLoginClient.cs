using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Availability.Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace Availability.Infrastructure.Auth;

/// <summary>
/// <c>POST /connect/token</c> (form-urlencoded) con <c>grant_type=password</c> o <c>authorization_code</c>. Credenciales o
/// código rechazados (400 <c>invalid_grant</c> o 401) → <c>null</c>; cualquier otro fallo → <see cref="AuthServerUnavailableException"/>.
/// Ni la contraseña ni el código ni el verifier se escriben en los logs.
/// </summary>
public sealed class AuthServerLoginClient(IHttpClientFactory httpClientFactory, ILogger<AuthServerLoginClient> logger)
    : IAuthServerLoginClient
{
    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string? AccessToken);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<string?> LoginAsync(string username, string password, string correlationId, CancellationToken ct = default) =>
        PostTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password
            },
            () => logger.LogInformation("Login rechazado por el Auth Server para el usuario {Username}.", username),
            ct);

    public Task<string?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, string clientId,
        string correlationId, CancellationToken ct = default) =>
        PostTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["code_verifier"] = codeVerifier,
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri
            },
            () => logger.LogInformation("El Auth Server rechazó el código de autorización (inválido, vencido o ya usado)."),
            ct);

    private async Task<string?> PostTokenAsync(Dictionary<string, string> form, Action logRejected, CancellationToken ct)
    {
        try
        {
            var http = httpClientFactory.CreateClient(AuthServerOptions.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "connect/token")
            {
                Content = new FormUrlEncodedContent(form)
            };
            //request.Headers.Add("X-Correlation-ID", correlationId);

            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                logRejected();
                return null;
            }

            if (!response.IsSuccessStatusCode)
                throw new AuthServerUnavailableException($"El Auth Server respondió {(int)response.StatusCode} al iniciar sesión.");

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct);
            if (string.IsNullOrWhiteSpace(body?.AccessToken))
                throw new AuthServerUnavailableException("El Auth Server devolvió una respuesta de token no interpretable.");

            return body.AccessToken;
        }
        catch (AuthServerUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException)
        {
            logger.LogError(ex, "No se pudo contactar al Auth Server para iniciar sesión.");
            throw new AuthServerUnavailableException("No se pudo contactar al Auth Server.", ex);
        }
    }
}
