using System.Linq.Expressions;
using Availability.Domain.Entities;

namespace Availability.Application.Abstractions.IRepository;

/// <summary>
/// Contrato BASE mínimo de persistencia. No se inyecta ni se consume directamente:
/// cada entidad define su propia interfaz (p. ej. <see cref="ISeatRepository"/>) que la extiende
/// y puede sumar comportamiento específico. La implementación compartida vive en un único
/// <c>GenericRepository&lt;T&gt;</c> abstracto en Infrastructure, para no duplicar código.
/// Las consultas se expresan por predicado (<c>Expression</c>) y se DEFINEN en clases estáticas por entidad dentro de
/// <c>Application/Specification</c> (p. ej. <c>SeatSpecification.ByEventAndNumber</c>) para reutilizarlas; no se usa el patrón
/// Specification completo (los repositorios siguen recibiendo la expresión, no una especificación).
/// </summary>
public interface IGenericRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    /// <summary>Marca la entidad como modificada en el tracker de EF. NO escribe en la base: la escritura ocurre en <see cref="SaveChangesAsync"/>.</summary>
    void Update(T entity);

    /// <summary>Marca la entidad como eliminada en el tracker de EF. NO escribe en la base: la escritura ocurre en <see cref="SaveChangesAsync"/>.</summary>
    void Remove(T entity);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
