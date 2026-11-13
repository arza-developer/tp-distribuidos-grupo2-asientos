using Availability.Application.Abstractions;

namespace Availability.Tests.Support;

/// <summary>Usuario autenticado fijo para los tests de los casos de uso (<c>null</c> = sin usuario/token de servicio).</summary>
public sealed class FakeCurrentUserAccessor(Guid? userId) : ICurrentUserAccessor
{
    public Guid? UserId { get; } = userId;
}
