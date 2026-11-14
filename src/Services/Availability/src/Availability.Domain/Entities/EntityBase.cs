namespace Availability.Domain.Entities;

/// <summary>
/// Base de todas las entidades persistidas.
/// <para>
/// <see cref="Id"/> es la identidad INTERNA de la base de datos local (clave primaria autoincremental).
/// Nunca debe salir del servicio: ni en DTOs, ni en eventos del broker, ni en llamadas a Booking.
/// Las referencias hacia afuera usan un identificador Guid propio de cada entidad (p. ej. <c>Seat.SeatId</c>),
/// de modo que una caída del Broker o de Booking no pueda dejar referencias inconsistentes con la BBDD local.
/// </para>
/// </summary>
public abstract class EntityBase
{
    public int Id { get; set; }
}
