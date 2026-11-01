using System.Linq.Expressions;
using Availability.Application.Abstractions;
using Availability.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Availability.Infrastructure.Persistence;

/// <summary>
/// Implementación base compartida (abstracta: no se instancia ni se registra en DI).
/// Cada repositorio concreto (p. ej. <see cref="SeatRepository"/>) hereda de ella y agrega sus consultas.
/// Lecturas sin tracking.
/// </summary>
public abstract class GenericRepository<T>(AvailabilityDbContext db) : IGenericRepository<T> where T : EntityBase
{
    protected AvailabilityDbContext Db { get; } = db;

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Db.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Db.Set<T>().AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        IQueryable<T> query = Db.Set<T>().AsNoTracking();
        if (predicate is not null) query = query.Where(predicate);
        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(T entity, CancellationToken ct = default) => await Db.Set<T>().AddAsync(entity, ct);

    public void Update(T entity) => Db.Set<T>().Update(entity);

    public void Remove(T entity) => Db.Set<T>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Db.SaveChangesAsync(ct);
}
