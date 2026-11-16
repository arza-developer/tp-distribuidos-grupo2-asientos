namespace Availability.Domain.Exceptions;

/// <summary>Categoría: el usuario autenticado no puede realizar la operación sobre ese recurso (Web lo traduce a 403).</summary>
public abstract class BusinessForbiddenException(string code, string message) : BusinessException(code, message);
