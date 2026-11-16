using MediatR;

namespace Availability.Application.Seats.Queries.GetSeats;

/// <summary>Query (lectura, CQRS): catálogo de asientos, opcionalmente filtrado por evento.</summary>
public sealed record GetSeatsQuery(Guid? EventId = null) : IRequest<IReadOnlyList<SeatDto>>;
