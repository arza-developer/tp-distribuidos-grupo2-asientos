using Availability.Application.Abstractions.IRepository;
using Availability.Domain.Entities;

namespace Availability.Infrastructure.Persistence.Repository;

public class SeatRequestRepository(AvailabilityDbContext db) : GenericRepository<SeatRequest>(db), ISeatRequestRepository;
