using Availability.Application.Abstractions;
using Availability.Application.Abstractions.IRepository;
using Availability.Application.Specification;
using MediatR;

namespace Availability.Application.Reservations.Queries.GetMyReservations;

/// <summary>
/// Usa el <c>ListAsync(predicado)</c> genérico con <see cref="SeatRequestSpecification.ByUser"/> (sin métodos nuevos en el
/// repositorio). Sin usuario (p. ej. token de servicio) no hay reservas propias: lista vacía. Si el usuario tiene varias
/// solicitudes sobre el mismo asiento (p. ej. una rechazada y luego una nueva) se informa la más reciente.
/// </summary>
public sealed class GetMyReservationsQueryHandler(ISeatRequestRepository requests, ICurrentUserAccessor currentUser)
    : IRequestHandler<GetMyReservationsQuery, IReadOnlyList<MyReservationItem>>
{
    public async Task<IReadOnlyList<MyReservationItem>> Handle(GetMyReservationsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return [];

        var mine = await requests.ListAsync(SeatRequestSpecification.ByUser(userId), ct);

        return mine
            .GroupBy(r => (r.EventId, r.SeatNumber))
            .Select(g => g.OrderByDescending(r => r.RequestedAtUtc).First())
            .Select(r => new MyReservationItem(r.ReservationId, r.EventId, r.SeatNumber, r.Status))
            .ToList();
    }
}
