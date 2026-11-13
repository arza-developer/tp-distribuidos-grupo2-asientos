namespace Availability.Domain.Exceptions;

/// <summary>
/// Categoría: los datos o la regla de negocio son inválidos (Web lo traduce a 400).
/// No se llama ValidationException para no chocar con System.ComponentModel.DataAnnotations.
/// </summary>
public abstract class BusinessValidationException(string code, string message) : BusinessException(code, message);
