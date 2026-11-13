namespace Availability.Application.Abstractions;

/// <summary>
/// Usuario autenticado de la operación en curso (claim <c>sub</c> del token, que para usuarios es su UserId Guid).
/// Lo implementa la capa Web. <c>null</c> si no hay usuario o el <c>sub</c> no es un Guid (p. ej. un token de servicio).
/// </summary>
public interface ICurrentUserAccessor
{
    Guid? UserId { get; }
}
