# 📋 Especificación Técnica: Tarea 09
**Rama Git:** `feature/09-observability-tracing`  
**Módulo:** `src/Common/SeatsReservation.Observability/`  
**Tipo:** Telemetría Distribuida y Logging  

## 1. Alcance y Responsabilidad
Implementar el mecanismo transversal de observabilidad para que un identificador de correlación (`CorrelationId` / `TraceId`) viaje a través de todas las fronteras de red (HTTP, colas de Azurite y llamadas binarias gRPC).

## 2. Requisitos de Cátedra
- **Requisito 3.f:** Logs de auditoría para identificar el inicio y fin de una cadena de sucesos en cada microservicio.
- **Observabilidad:** Trazabilidad distribuida, correlación de trazas y logging estructurado.

## 3. Contratos y Herramientas
- Librería: Serilog / OpenTelemetry en .NET 8.
- Cabecera HTTP: `X-Correlation-ID`.
- Propiedades de Mensajería: Metadata en mensajes de Azurite / Service Bus.
- Metadatos gRPC: `Metadata` de llamada en HTTP/2.

## 4. Tareas Concretas
1. Crear un Middleware ASP.NET Core reutilizable que capture o genere `X-Correlation-ID` en peticiones entrantes.
2. Inyectar el CorrelationId en el contexto de logging enriquecido (`LogContext`).
3. Crear interceptores gRPC y decoradores de mensajería para propagar el ID en llamadas salientes.
4. Asegurar que los logs de cada servicio muestren `[{CorrelationId}] INICIO: ...` y `[{CorrelationId}] FIN: ...`.

## 5. Criterios de Aceptación (DoD)
- [ ] Al ejecutar una reserva, se observa el mismo CorrelationId en la consola de Availability, Booking, Payment y Notifications.
- [ ] Ningún microservicio pierde el identificador en los saltos de red.

## 6. Defensa Oral
- **¿Por qué es indispensable la propagación de contexto en microservicios?**  
  *Respuesta:* Para evitar el problema de "vuelo a ciegas"; permite correlacionar logs de múltiples procesos aislados en una única transacción de usuario.
