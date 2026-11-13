namespace Availability.Application.Abstractions;

/// <summary>
/// Identificador de correlación de la operación en curso (trazabilidad de logs y mensajes, requisito 3.f).
/// Lo implementa la capa Web a partir de la request HTTP.
/// </summary>
public interface ICorrelationIdAccessor
{
    string CorrelationId { get; }
}
