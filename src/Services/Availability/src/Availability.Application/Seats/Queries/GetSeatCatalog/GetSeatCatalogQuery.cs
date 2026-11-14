using MediatR;

namespace Availability.Application.Seats.Queries.GetSeatCatalog;

/// <summary>
/// Query (lectura, CQRS) del catálogo para las PÁGINAS: igual que <c>GetSeatsQuery</c> pero incluyendo el
/// <c>EventId</c> de cada asiento (necesario para reservar desde la UI). <c>GetSeatsQuery</c>/<c>SeatDto</c>, el
/// contrato público de la API, no cambian.
/// </summary>
public sealed record GetSeatCatalogQuery(Guid? EventId = null) : IRequest<IReadOnlyList<SeatCatalogItem>>;

public sealed record SeatCatalogItem(Guid EventId, string SeatNumber, decimal Price, bool IsAvailable);
