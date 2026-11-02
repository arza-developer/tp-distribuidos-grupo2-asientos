using System.Linq.Expressions;
using Availability.Application.Abstractions.IRepository;
using Availability.Application.Seats.Queries.GetSeatCatalog;
using Availability.Domain.Entities;

namespace Availability.Tests.Unit;

public class GetSeatCatalogQueryHandlerTests
{
    /// <summary>Repositorio que falla si alguien usa el método específico: el handler debe usar el ListAsync genérico.</summary>
    private sealed class GenericOnlySeatRepository : InMemoryRepository<Seat>, ISeatRepository
    {
        public List<bool> PredicateWasProvided { get; } = [];

        public new Task<IReadOnlyList<Seat>> ListAsync(Expression<Func<Seat, bool>>? predicate = null, CancellationToken ct = default)
        {
            PredicateWasProvided.Add(predicate is not null);
            return base.ListAsync(predicate, ct);
        }

        public Task<IReadOnlyList<Seat>> ListByEventAsync(Guid? eventId, CancellationToken ct = default) =>
            throw new InvalidOperationException("El catálogo debe usar ListAsync(predicado), no ListByEventAsync.");
    }

    private static readonly Guid EventA = Guid.NewGuid();
    private static readonly Guid EventB = Guid.NewGuid();

    private static Seat NewSeat(Guid eventId, string number, decimal price = 15000m, bool available = true) => new()
    {
        SeatId = Guid.NewGuid(), EventId = eventId, SeatNumber = number, Price = price, IsAvailable = available
    };

    private static (GetSeatCatalogQueryHandler Handler, GenericOnlySeatRepository Repo) Create(params Seat[] seats)
    {
        var repo = new GenericOnlySeatRepository();
        repo.Items.AddRange(seats);
        return (new GetSeatCatalogQueryHandler(repo), repo);
    }

    [Fact]
    public async Task Handle_MapsEveryField_IncludingTheEventId()
    {
        var (handler, _) = Create(NewSeat(EventA, "A-3", 18000m, available: false));

        var item = Assert.Single(await handler.Handle(new GetSeatCatalogQuery(), default));

        Assert.Equal(new SeatCatalogItem(EventA, "A-3", 18000m, false), item);
    }

    [Fact]
    public async Task Handle_WithoutEventId_ReturnsTheSeatsOfEveryEvent()
    {
        var (handler, _) = Create(NewSeat(EventA, "A-1"), NewSeat(EventB, "B-1"));

        var result = await handler.Handle(new GetSeatCatalogQuery(), default);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.EventId == EventA && i.SeatNumber == "A-1");
        Assert.Contains(result, i => i.EventId == EventB && i.SeatNumber == "B-1");
    }

    [Fact]
    public async Task Handle_WithEventId_FiltersToThatEvent()
    {
        var (handler, _) = Create(NewSeat(EventA, "A-1"), NewSeat(EventA, "A-2"), NewSeat(EventB, "B-1"));

        var result = await handler.Handle(new GetSeatCatalogQuery(EventA), default);

        Assert.Equal(["A-1", "A-2"], result.Select(i => i.SeatNumber));
        Assert.All(result, i => Assert.Equal(EventA, i.EventId));
    }

    [Fact]
    public async Task Handle_UnknownEvent_ReturnsEmpty()
    {
        var (handler, _) = Create(NewSeat(EventA, "A-1"));

        Assert.Empty(await handler.Handle(new GetSeatCatalogQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_EmptyCatalog_ReturnsEmpty()
    {
        var (handler, _) = Create();

        Assert.Empty(await handler.Handle(new GetSeatCatalogQuery(), default));
    }

    [Fact]
    public async Task Handle_OrdersBySeatNumber_Ordinally()
    {
        var (handler, _) = Create(NewSeat(EventA, "B-1"), NewSeat(EventA, "A-10"), NewSeat(EventA, "A-2"), NewSeat(EventA, "A-1"));

        var result = await handler.Handle(new GetSeatCatalogQuery(), default);

        Assert.Equal(["A-1", "A-10", "A-2", "B-1"], result.Select(i => i.SeatNumber));
    }

    [Fact]
    public async Task Handle_UsesTheGenericListAsyncWithAPredicate_NeverTheSpecificRepositoryMethod()
    {
        var (handler, repo) = Create(NewSeat(EventA, "A-1"));

        await handler.Handle(new GetSeatCatalogQuery(EventA), default);
        await handler.Handle(new GetSeatCatalogQuery(), default);

        // Si usara ListByEventAsync el repositorio lanzaría; además el predicado viaja siempre (la query se define en el handler).
        Assert.Equal([true, true], repo.PredicateWasProvided);
    }
}
