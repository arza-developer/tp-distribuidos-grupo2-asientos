using Availability.Application.Abstractions.IRepository;
using Availability.Application.Specification;
using MediatR;

namespace Availability.Application.Seats.Queries.GetSeatCatalog;

/// <summary>
/// Catálogo de asientos con su evento. Usa el <c>ListAsync(predicado)</c> GENÉRICO de <c>IGenericRepository</c> (heredado por
/// <c>ISeatRepository</c>) con la consulta definida en <see cref="SeatSpecification"/>: no se agregan métodos al repositorio.
/// </summary>
public sealed class GetSeatCatalogQueryHandler(ISeatRepository seats)
    : IRequestHandler<GetSeatCatalogQuery, IReadOnlyList<SeatCatalogItem>>
{
    public async Task<IReadOnlyList<SeatCatalogItem>> Handle(GetSeatCatalogQuery request, CancellationToken ct)
    {
        var result = await seats.ListAsync(SeatSpecification.ForEvent(request.EventId), ct);

        return result
            .OrderBy(s => s.SeatNumber, StringComparer.Ordinal)
            .Select(s => new SeatCatalogItem(s.EventId, s.SeatNumber, s.Price, s.IsAvailable))
            .ToList();
    }
}
