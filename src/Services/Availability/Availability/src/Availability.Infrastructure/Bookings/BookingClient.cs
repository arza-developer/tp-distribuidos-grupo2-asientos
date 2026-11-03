using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Availability.Application.Abstractions;
using Availability.Application.Bookings;
using Availability.Application.Exceptions;
using Availability.Domain.Enums;

namespace Availability.Infrastructure.Bookings;

/// <summary>
/// Cliente REST de solo lectura hacia Booking (paso 7 del TP: GET /api/bookings/{id}).
/// ANTI-CORRUPTION LAYER: aquí, y solo aquí, se traduce el vocabulario de Booking (estados en español) al enum de
/// Availability. Booking responde 404 cuando todavía no procesó el mensaje de la cola → se devuelve <c>null</c>.
/// Cualquier otro fallo (red, timeout, 5xx, JSON/estado no interpretable) es técnico → <see cref="BookingUnavailableException"/>.
/// </summary>
public sealed class BookingClient(HttpClient http, ICorrelationIdAccessor correlation) : IBookingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, ReservationStatus> StatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Retenida"] = ReservationStatus.Retained,
        ["Confirmada"] = ReservationStatus.Confirmed,
        ["Rechazada"] = ReservationStatus.Rejected,
        ["Vencida"] = ReservationStatus.Expired
    };

    private sealed record BookingResponse(string? Status, string? SeatNumber, DateTime? ExpiresAtUtc);

    public async Task<BookingStatusInfo?> GetStatusAsync(Guid reservationId, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/bookings/{reservationId}");
            request.Headers.Add("X-Correlation-ID", correlation.CorrelationId);

            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new BookingUnavailableException($"Booking respondió {(int)response.StatusCode}.");

            var body = await response.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions, ct);
            if (body?.Status is null || !StatusMap.TryGetValue(body.Status, out var status))
                throw new BookingUnavailableException($"Booking devolvió un estado no reconocido: '{body?.Status}'.");

            return new BookingStatusInfo(status, body.ExpiresAtUtc, body.SeatNumber ?? string.Empty);
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
