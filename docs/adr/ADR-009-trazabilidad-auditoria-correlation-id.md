# ADR-009: Trazabilidad Distribuida y Auditoría mediante CorrelationId

**Estado:** Aprobado  
**Fecha:** 2026-11-01  

## 1. Contexto y Restricciones
- **Requisito 3.f:** *"Agregar logs de auditoría para identificar el inicio y fin de una cadena de sucesos en cada microservicio"*.
- **Topología:** Solicitudes que inician en HTTP (`Availability`), transitan por colas asíncronas (`Azure Service Bus`), ejecutan RPC binario (`Payment`) y emiten eventos (`Notifications`).

## 2. Alternativas Evaluadas

### Opción A: Logs Aislados sin Identificador Común
- **Mecanismo:** Cada microservicio emite logs con sus propios timestamps locales y formatos independientes.
- **Desventajas:** Imposibilidad de correlacionar eventos ante fallos distribuidos. En ráfagas concurrentes, los logs de múltiples reservas se mezclan sin criterio de causalidad. Viola el Requisito 3.f.

### Opción B: Infraestructura Completa de OpenTelemetry con Colector Central
- **Mecanismo:** Instrumentación automática con exportadores OTLP hacia Jaeger o Zipkin.
- **Desventajas:** Sobrecarga de configuración y despliegue de contenedores adicionales de telemetría no requeridos por la consigna del trabajo práctico.

### Opción C: Contexto Transversal de Correlación con Serilog LogContext (Seleccionada)
- **Mecanismo:** Propagación explícita de un identificador único (`X-Correlation-Id`) a través de todos los límites de transporte (HTTP headers, gRPC metadata y Application Properties de Service Bus).
- **Ventajas:** Liviano, integrable en el pipeline de ASP.NET Core mediante middleware e interceptores, y auditable directamente en los logs de consola de Docker.

## 3. Decisión
Se implementa la biblioteca transversal **`SeatsReservation.Observability`**:
1. **Punto de Ingesta:** `CorrelationIdMiddleware` en `Availability` captura el encabezado `X-Correlation-Id` o genera un nuevo `Guid` al recibir la solicitud, inyectándolo en el `LogContext` de Serilog.
2. **Propagación en Broker:** El publicador inyecta `CorrelationId` en las propiedades de aplicación del mensaje de Service Bus. Los workers de `Booking` lo extraen al desencolar.
3. **Propagación en gRPC:** `GrpcCorrelationInterceptor` inyecta `x-correlation-id` en los metadatos de llamada hacia `Payment`.
4. **Auditoría de Cadena de Sucesos:** Cada microservicio emite un log estructurado al iniciar y al finalizar su etapa en la cadena, prefijado con el componente y el `CorrelationId`.

## 4. Costo de Mantenimiento y Mitigaciones
- **Sobrecarga de Código:** Exige que cada llamada saliente (HTTP, gRPC, Broker) propague activamente el contexto.  
  *Mitigación:* Se encapsula en delegados de `HttpClient`, interceptores gRPC y extensiones de mensajería para que ocurra de forma transparente a la lógica de negocio.
