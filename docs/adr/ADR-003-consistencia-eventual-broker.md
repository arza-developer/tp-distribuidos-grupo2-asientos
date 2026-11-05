# ADR-003: Frontera de Consistencia Eventual y Desacoplamiento con Broker

**Estado:** Aprobado  
**Fecha:** 2026-10-26  

## 1. Contexto y Restricciones
- **Flujo de Reserva (Pasos 3 y 4):** `Availability` recibe la solicitud, verifica existencia local, publica en el broker y responde de inmediato sin esperar el cobro. Una réplica de `Booking` procesa en segundo plano.
- **Requisito 3.g:** Indicar en el diseño la frontera de consistencia eventual.
- **Carga Concurrente:** Picos de solicitudes sobre asientos limitados no deben agotar los hilos de atención del catálogo.

## 2. Alternativas Evaluadas

### Opción A: Comunicación Sincrónica HTTP (Availability -> Booking)
- **Ventajas:** El cliente recibe confirmación o rechazo inmediato en la misma llamada HTTP.
- **Desventajas:** Acoplamiento temporal estricto. Durante ráfagas masivas, el ThreadPool de `Availability` colapsa atendiendo conexiones en espera. La caída de Booking o Payment indisponibiliza el catálogo.

### Opción B: Transacción Distribuida Two-Phase Commit (2PC)
- **Ventajas:** Atomicidad absoluta inmediata entre catálogo y reserva.
- **Desventajas:** Bloqueo de recursos a través de la red durante la fase *Prepare*. Sacrifica disponibilidad ante particiones de red (Teorema CAP). Rendimiento nulo ante alta concurrencia.

### Opción C: Desacoplamiento Asíncrono con Broker (Modelo AP de Brewer) (Seleccionada)
- **Ventajas:** `Availability` responde de inmediato `HTTP 202 Accepted` y encola en Azure Service Bus. El broker actúa como amortiguador; las réplicas de `Booking` procesan según su propia capacidad.
- **Desventajas:** El cliente no conoce el resultado de inmediato.

## 3. Decisión
1. Se establece la **Frontera de Consistencia Eventual** en la publicación del mensaje en el broker. `Availability` responde `HTTP 202 Accepted` con cabecera `Location`. El cliente realiza seguimiento consultando `GET /api/reservations/{id}`.
2. **Ordenamiento de Eventos:** Se delega el ordenamiento temporal a la cola FIFO del broker y al identificador de correlación (`CorrelationId`), evitando la sobrecarga de implementar relojes vectoriales en este límite.
3. **Invariante Fail-Safe:** Si el broker falla o carece de credenciales, `Availability` no finge aceptación: retorna `HTTP 503 Service Unavailable` y revierte el estado local del asiento.

## 4. Costo de Mantenimiento y Mitigaciones
- **Mensajes Duplicados:** Reintentos del broker pueden entregar el mismo comando más de una vez.  
  *Mitigación:* `BookingService` aplica control de idempotencia validando el `RequestId` único antes de procesar la reserva.
