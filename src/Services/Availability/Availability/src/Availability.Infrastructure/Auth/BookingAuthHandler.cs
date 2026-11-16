using System.Net;
using System.Net.Http.Headers;
using Availability.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Availability.Infrastructure.Auth;

/// <summary>
/// Requisito 3.e entre servicios: agrega <c>Authorization: Bearer {token M2M}</c> a cada llamada a Booking.
/// Si Booking responde 401 (token vencido o revocado) descarta el token, pide uno nuevo y reintenta UNA vez; si vuelve a
/// responder 401 se devuelve esa respuesta (sin bucles). El reintento solo aplica a pedidos sin cuerpo (los GET de estado).
/// <para>
/// LIMITACIÓN (L6): hoy Booking no valida tokens, así que ignora el header; esto deja listo el cliente para cuando lo haga.
/// </para>
/// </summary>
public sealed class BookingAuthHandler(
    IAccessTokenProvider tokens,
    ICorrelationIdAccessor correlation,
    ILogger<BookingAuthHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await tokens.GetTokenAsync(correlation.CorrelationId, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized || request.Content is not null)
            return response;

        logger.LogWarning("Booking respondió 401 al token M2M: se renueva el token y se reintenta una vez.");
        response.Dispose();
        tokens.Invalidate(token);

        var fresh = await tokens.GetTokenAsync(correlation.CorrelationId, ct);
        using var retry = Clone(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fresh);
        return await base.SendAsync(retry, ct);
    }

    private static HttpRequestMessage Clone(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
        foreach (var header in original.Headers.Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
