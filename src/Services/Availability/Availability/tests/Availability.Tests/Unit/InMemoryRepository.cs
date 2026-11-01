using System.Linq.Expressions;
using Availability.Application.Abstractions.IRepository;
using Availability.Domain.Entities;

namespace Availability.Tests.Unit;

/// <summary>Fakes escritos a mano: lista en memoria que evalúa los predicados.</summary>
public abstract class InMemoryRepository<T> : IGenericRepository<T> where T : EntityBase
{
    public List<T> Items { get; } = [];

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(e => e.Id == id));

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(predicate.Compile()));

    public Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        IEnumerable<T> query = Items;
        if (predicate is not null) query = query.Where(predicate.Compile());
        return Task.FromResult<IReadOnlyList<T>>(query.ToList());
    }

    public Task AddAsync(T entity, CancellationToken ct = default)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity) { }

    public void Remove(T entity) => Items.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
}

public sealed class InMemorySeatRepository : InMemoryRepository<Seat>, ISeatRepository
{
    public Task<IReadOnlyList<Seat>> ListByEventAsync(Guid? eventId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Seat>>(Items.Where(s => eventId == null || s.EventId == eventId).ToList());
}

public sealed class InMemorySeatRequestRepository : InMemoryRepository<SeatRequest>, ISeatRequestRepository;
