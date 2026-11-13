namespace Availability.Domain.Exceptions;

/// <summary>Categoría: la operación choca con el estado actual del recurso (Web lo traduce a 409).</summary>
public abstract class BusinessConflictException(string code, string message) : BusinessException(code, message);
