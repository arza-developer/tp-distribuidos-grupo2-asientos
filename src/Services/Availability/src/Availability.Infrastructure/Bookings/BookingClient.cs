using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Availability.Application.Abstractions;
using Availability.Application.Bookings;
using Availability.Application.Exceptions;
using Availability.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Availability.Infrastructure.Bookings;

/// <summary>
/// Cliente REST de solo lectura hacia Booking (paso 7 del TP: GET /api/bookings/{id}).
/// ANTI-CORRUPTION LAYER: aquí, y solo aquí, se traduce el vocabulario de Booking (estados <c>Retained/Confirmed/Rejected/Expired</c>,
/// tal como los emite su controller) al enum de Availability. Booking responde 404 cuando todavía no procesó el mensaje del
/// broker → se devuelve <c>null</c>. Cualquier otro fallo (red, timeout, 5xx, JSON/estado no interpretable) es técnico →
/// <see cref="BookingUnavailableException"/>. Propaga el <c>X-Correlation-ID</c> (req. 3.f) para que ambos servicios logueen
/// la misma operación bajo el mismo id.
/// </summary>
public sealed class BookingClient(HttpClient http, ICorrelationIdAccessor correlation, ILogger<BookingClient> logger) : IBookingClient
{
    private const string CorrelationHeader = "X-Correlation-ID";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, ReservationStatus> StatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Retained"] = ReservationStatus.Retained,
        ["Confirmed"] = ReservationStatus.Confirmed,
        ["Rejected"] = ReservationStatus.Rejected,
        ["Expired"] = ReservationStatus.Expired
    };

    private sealed record BookingResponse(string? Status, string? SeatNumber, DateTime? ExpiresAtUtc, string? RejectionReason);

    public async Task<BookingStatusInfo?> GetStatusAsync(Guid reservationId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/bookings/{reservationId}");
            request.Headers.Add(CorrelationHeader, correlation.CorrelationId);

            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new BookingUnavailableException($"Booking respondió {(int)response.StatusCode}.");

            var body = await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions, ct);
            if (body?.Status is null || !StatusMap.TryGetValue(body.Status, out var status))
            {
                // Sin el valor recibido sería imposible distinguir una deriva de contrato de una falla de red.
                logger.LogWarning("Booking respondió {StatusCode} con un estado no reconocido: '{BookingStatus}'.",
                    (int)response.StatusCode, body?.Status);
                throw new BookingUnavailableException($"Booking devolvió un estado no reconocido: '{body?.Status}'.");
            }

            return new BookingStatusInfo(status, body.ExpiresAtUtc, body.SeatNumber ?? string.Empty,
                string.IsNullOrWhiteSpace(body.RejectionReason) ? null : body.RejectionReason);
        }
        catch (BookingUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // cancelación del llamador: no es un fallo de Booking
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException)
        {
            // OperationCanceledException sin cancelación del llamador = timeout del HttpClient
            throw new BookingUnavailableException("No se pudo consultar a Booking.", ex);
        }
    }
}
