using Availability.Domain.Enums;
using MediatR;

namespace Availability.Application.Reservations.Queries.GetMyReservations;

/// <summary>
/// Reservas del usuario autenticado (el usuario sale del token, nunca del pedido). Devuelve la ÚLTIMA solicitud del usuario por
/// asiento, para que la página de inicio pueda enlazar cada asiento con el estado de su reserva.
/// </summary>
public sealed record GetMyReservationsQuery : IRequest<IReadOnlyList<MyReservationItem>>;

public sealed record MyReservationItem(Guid ReservationId, Guid EventId, string SeatNumber, ReservationStatus Status);
