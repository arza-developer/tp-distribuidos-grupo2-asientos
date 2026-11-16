namespace Availability.Domain.Exceptions;

/// <summary>Categoría: el recurso de negocio referenciado no existe (Web lo traduce a 404).</summary>
public abstract class BusinessNotFoundException(string code, string message) : BusinessException(code, message);
