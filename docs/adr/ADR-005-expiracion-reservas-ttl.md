# ADR-005: Ciclo de Vida de Reservas y Mecanismo de Expiración TTL

**Estado:** Aprobado  
**Fecha:** 2026-10-27  

## 1. Contexto y Restricciones
- Si una reserva retenida no completa el pago dentro de su ventana de vigencia, debe pasar a estado `Expired` y liberar el asiento.
- Regla de negocio: un asiento solo se considera bloqueado si tiene una reserva en estado `Retained` o `Confirmed`.

## 2. Alternativas Evaluadas

### Opción A: Expiración Pasiva en Lectura
- La reserva se evalúa solo cuando un usuario consulta el estado o intenta reservar el mismo asiento.
- Desventajas: si nadie consulta el asiento, la reserva queda retenida indefinidamente en base de datos, distorsionando la disponibilidad real.

### Opción B: Eventos de Expiración vía Redis TTL
- Uso de llaves con tiempo de vida en caché externa y callbacks reactivos.
- Desventajas: agrega un componente de infraestructura adicional no requerido por el alcance del trabajo.

### Opción C: Tarea Periódica en Background (Seleccionada)
- Un `BackgroundService` dentro de `Booking.Api` barre periódicamente las reservas vencidas.
- Ventajas: autónomo, no agrega dependencias externas y resuelve la consistencia directamente en SQLite.

## 3. Decisión
Se implementa `ReservationExpirationWorker` como `BackgroundService`:
1. **Frecuencia:** se ejecuta cada 15 segundos utilizando `PeriodicTimer` (`ReservationExpiration:IntervalSeconds` en `appsettings.json`).
2. **TTL de Retención:** el tiempo de retención se fija en 10 minutos (`ExpiresAtUtc = now.AddMinutes(10)` al retener).
3. **Operación Atómica:** ejecuta `ExpireOverdueReservationsAsync` actualizando en bloque las reservas con `Status == Retained` y `ExpiresAtUtc <= UtcNow` al estado `Expired`.
4. El asiento queda inmediatamente disponible para nuevas solicitudes por efecto del índice único filtrado.

## 4. Costo de Mantenimiento y Mitigaciones
- **Ventana de Retraso:** existe un lapso de hasta 15 segundos entre el vencimiento formal y la ejecución del barrido en base.
  *Mitigación:* en `BookingService.ProcessBookingAsync` se valida explícitamente `ExpiresAtUtc <= UtcNow` antes de invocar el cobro gRPC, evitando procesar pagos sobre reservas ya vencidas.
