# ADR-008: Desacoplamiento del Servicio de Notificaciones mediante Eventos

**Estado:** Aprobado  
**Fecha:** 2026-10-31  

## 1. Contexto y Restricciones
- **Requisito 3.h:** Notificar al usuario cuando la reserva esté creada y confirmada tras un cobro exitoso.
- **Aislamiento de Fallos:** El envío de notificaciones no debe interferir con la transacción financiera ni con la disponibilidad de la máquina de estados.

## 2. Alternativas Evaluadas

### Opción A: Invocación Sincrónica desde Payment
- **Ventajas:** Notificación despachada inmediatamente tras confirmar el pago en base de datos.
- **Desventajas:** Viola el principio de responsabilidad única: la pasarela financiera asume dependencias con canales de comunicación con usuarios. Si el despachador de alertas falla, degrada o aborta la respuesta de cobro gRPC.

### Opción B: Invocación Sincrónica desde Booking
- **Ventajas:** Mantiene el control dentro del orquestador de reservas.
- **Desventajas:** Agrega latencia bloqueante en el worker transaccional antes de responder o liberar hilos. Fallos en el proveedor de notificaciones pueden generar inconsistencias en la máquina de estados.

### Opción C: Consumidor Asíncrono Autónomo basado en Eventos (Seleccionada)
- **Ventajas:** `Booking` confirma la reserva en base de datos y emite el evento de dominio `ReservationConfirmed`. `Notifications.Worker` consume el evento en segundo plano. Si el worker falla, la reserva sigue confirmada y el cobro intacto.
- **Desventajas:** Introducción de consistencia eventual en la comunicación hacia el usuario.

## 3. Decisión
Se implementa **`Notifications.Worker`** como un microservicio autónomo en Docker (`seats-notifications-worker`):
1. Opera como un `BackgroundService` en .NET que escucha eventos de reservas confirmadas de forma desacoplada.
2. `Payment` y `Booking` no dependen de la disponibilidad ni de los tiempos de respuesta del servicio de notificaciones.
3. El worker propaga la trazabilidad extrayendo el `CorrelationId` del evento e inyectándolo en el `LogContext` de Serilog para auditoría (Requisito 3.f).

## 4. Costo de Mantenimiento y Mitigaciones
- **Desfase Temporal:** El usuario puede consultar el estado confirmado antes de recibir la alerta.  
  *Mitigación:* La verdad transaccional reside en la base de datos consultable vía API (`GET /api/reservations/{id}`); la notificación actúa como canal complementario y no como barrera de sincronización.
