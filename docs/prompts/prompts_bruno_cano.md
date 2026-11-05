# Historial de Prompts - Bruno Cano
**Rol:** Tarea 09 - Observabilidad, Trazabilidad y Middleware Transversal  
**Rama:** `feature/09-observability-tracing`

### Contexto y Desafíos Reales
Creación de la biblioteca común `SeatsReservation.Observability` para propagar el `CorrelationId` entre HTTP, Azure Service Bus y gRPC sin ensuciar la lógica de negocio.

### Prompts Clave Utilizados

#### Prompt 1: ¿Se Requiere Base de Datos para Observabilidad?
> "En el chat del grupo pregunté si para la Tarea 09 tenía que crear una base de datos propia para persistir logs o alcanzaba con Serilog, CorrelationId y OpenTelemetry. Confirmamos que no hace falta DB. ¿Cómo armo una librería de clase compartida (`SeatsReservation.Observability`) que provea un Middleware HTTP para capturar o generar `X-Correlation-Id`?"

#### Prompt 2: Propagación de CorrelationId en Llamadas gRPC
> "En HTTP uso HttpContext, pero cuando Booking llama a Payment por gRPC el contexto se pierde. ¿Cómo escribo un `ClientInterceptor` de gRPC en C# que tome el CorrelationId del scope actual y lo agregue a los `Metadata` salientes como encabezado `x-correlation-id`?"

#### Prompt 3: Propagación en Mensajes de Azure Service Bus
> "Cuando Availability manda el comando de reserva a la cola de Azure Service Bus, no quiero meter el CorrelationId adentro del JSON del body para no acoplar los contratos. ¿Cómo lo inyecto en las `ApplicationProperties` del `ServiceBusMessage` y cómo lo recupero en el consumidor de Booking?"
