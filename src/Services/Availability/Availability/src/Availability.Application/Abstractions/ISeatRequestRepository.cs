using Availability.Domain.Entities;

namespace Availability.Application.Abstractions;

/// <summary>
/// Repositorio de solicitudes de reserva. Por ahora solo la base genérica; las consultas propias
/// (p. ej. por <c>ReservationId</c>) se agregan cuando un caso de uso las necesite.
/// </summary>
public interface ISeatRequestRepository : IGenericRepository<SeatRequest>;
