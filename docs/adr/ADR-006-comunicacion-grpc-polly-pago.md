# ADR-006: Comunicación gRPC y Resiliencia con Polly (Booking -> Payment)

**Estado:** Aprobado  
**Fecha:** 2026-10-27  

## 1. Contexto y Restricciones
- `Booking.Api` debe comunicarse sincrónicamente con `Payment.Grpc` para procesar el cobro de la reserva retenida.
- Si el servicio de pagos falla o no responde, el sistema debe proteger los recursos de ejecución y evitar inconsistencias financieras.

## 2. Alternativas Evaluadas

### Opción A: API REST HTTP/1.1 sin Políticas de Reintento
- Llamadas sincrónicas JSON estándar.
- Desventajas: sobrecarga de serialización, bloqueo de threads ante caídas del receptor y falta de contratos estrictos.

### Opción B: Falla Rápida Inmediata (Fail-Fast con Abort Directo)
- Al primer fallo o timeout del cliente gRPC, se marca la reserva como `Rejected` y se descarta el mensaje.
- Desventajas: ante microcortes de red transitorios o reinicios breves del contenedor de pagos, descarta reservas válidas sin dar margen de recuperación.

### Opción C: Pipeline Polly v8 y Reintento Diferido en Broker (Seleccionada)
- Protección del cliente gRPC con Polly (Retry + Circuit Breaker) combinado con liberación de mensaje en Azure Service Bus (`AbandonMessageAsync`) ante indisponibilidad del servicio.

## 3. Decisión
1. **Protocolo:** gRPC sobre HTTP/2 (`payment.proto`), con tipado estricto mediante Protocol Buffers.
2. **Políticas de Resiliencia (Polly v8):**
   - **Retry:** 2 intentos con backoff exponencial y jitter (retraso base de 200 ms).
   - **Circuit Breaker:** apertura al alcanzar 50% de fallos sobre 5 llamadas mínimas en una ventana de 30 segundos; duración de corte de 15 segundos.
   - **Timeout:** 2 segundos por intento.
3. **Manejo de Caída (Eventual Recovery):**
   - Si Payment devuelve `Unavailable`, timeout o el circuito está abierto, `PaymentClient` captura la excepción técnica y retorna resultado `Unavailable`.
   - `BookingService` eleva `PaymentUnavailableException`.
   - El consumidor `BookingQueueConsumer` ejecuta `AbandonMessageAsync`, devolviendo el mensaje a la cola para permitir su reintento una vez restablecido el servicio.
   - La reserva permanece en estado `Retained` hasta que el mensaje sea reprocesado exitosamente o venza su TTL de 10 minutos vía `ReservationExpirationWorker`.

## 4. Costo de Mantenimiento y Mitigaciones
- **Retención Prolongada:** si la pasarela de pagos permanece caída, el asiento queda bloqueado durante la ventana del TTL.
  *Mitigación:* el intervalo de 10 minutos garantiza que el asiento se libere de forma autónoma sin intervención manual si el servicio no se recupera.
