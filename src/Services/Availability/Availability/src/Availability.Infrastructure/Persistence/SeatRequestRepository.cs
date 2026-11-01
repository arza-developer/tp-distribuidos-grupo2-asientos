using Availability.Application.Abstractions;
using Availability.Domain.Entities;

namespace Availability.Infrastructure.Persistence;

public class SeatRequestRepository(AvailabilityDbContext db) : GenericRepository<SeatRequest>(db), ISeatRequestRepository;
