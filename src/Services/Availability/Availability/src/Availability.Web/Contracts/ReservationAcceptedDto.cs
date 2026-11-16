namespace Availability.Web.Contracts;

/// <summary>Respuesta 202: la solicitud fue recibida y encolada; el resultado final es eventual.</summary>
public sealed record ReservationAcceptedDto(Guid ReservationId, string Status, string Message, string CheckStatusUrl);
