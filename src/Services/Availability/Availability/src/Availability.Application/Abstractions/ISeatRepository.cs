using Availability.Domain.Entities;

namespace Availability.Application.Abstractions;

/// <summary>Repositorio de asientos: base genérica + consultas propias de <see cref="Seat"/>.</summary>
public interface ISeatRepository : IGenericRepository<Seat>
{
    /// <summary>Asientos de un evento; con <paramref name="eventId"/> nulo devuelve los de todos los eventos.</summary>
    Task<IReadOnlyList<Seat>> ListByEventAsync(Guid? eventId, CancellationToken ct = default);
}
