using Availability.Application.Abstractions;

namespace Availability.Web.Services;

/// <summary>
/// Usuario actual a partir del claim <c>sub</c> (el JWT se valida con <c>MapInboundClaims=false</c>, así que el claim se
/// llama "sub"). Para tokens de usuario es el UserId Guid; para tokens de servicio es un ClientId y se informa sin usuario.
/// </summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor http) : ICurrentUserAccessor
{
    public Guid? UserId =>
        Guid.TryParse(http.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
}
