namespace Availability.Domain.Exceptions;

/// <summary>
/// Raíz de los errores de NEGOCIO (una regla o validación del dominio no se cumple).
/// <para>
/// Vive en Domain porque solo depende de la BCL y es vocabulario del dominio; no agrega comportamiento a las
/// entidades (siguen anémicas): los casos de uso (Application) son quienes la lanzan.
/// No expone códigos HTTP (preocupación de la capa exterior): Web traduce por categoría
/// (<see cref="BusinessNotFoundException"/>, <see cref="BusinessValidationException"/>).
/// </para>
/// <para>
/// Los fallos TÉCNICOS (broker caído, BBDD) NO son de negocio y no heredan de esta clase.
/// </para>
/// </summary>
public abstract class BusinessException(string code, string message) : Exception(message)
{
    /// <summary>Identificador estable y legible por máquina, p. ej. <c>seat.not_found</c>.</summary>
    public string Code { get; } = code;
}
