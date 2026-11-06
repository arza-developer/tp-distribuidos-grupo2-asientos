# Historial de Prompts - Martín Velázquez

**Rol:** Tarea 04 - Payment Service (gRPC, HTTP/2 y Simulación de Fallos)  
**Rama:** `feature/04-payment-grpc-service`

## Contexto y Desafíos Reales
Diseño del contrato Protobuf, configuración de Kestrel para HTTP/2 nativo y switch de simulación de fallas para pruebas de caos.

## Prompts Clave Utilizados

### Prompt 1: Contrato Protobuf y Configuración de Proyecto en .NET 8
> "Necesito armar la estructura inicial para `Payment.Grpc` en .NET 8 con Clean Architecture (Domain, DataAccess, Business). ¿Cómo estructuro el archivo `payment.proto` con `syntax proto3` para el método `ProcessPayment` y qué configuración le tengo que poner al `.csproj` para que compile el servidor gRPC?"

### Prompt 2: Forzar Protocolo HTTP/2 en Kestrel para gRPC
> "Al intentar levantar el servicio gRPC de pagos, Kestrel intenta negociar HTTP/1.1 por defecto. ¿Cómo configuro el `Program.cs` para forzar exclusivamente `HttpProtocols.Http2` en el endpoint de Payment?"

### Prompt 3: Chaos Switch (SimulateFailure) para Pruebas de Resiliencia
> "Para el Requisito 3.j de la cátedra necesitamos simular caídas de la pasarela de pagos sin tener que apagar el contenedor siempre. Quiero agregar una sección en `appsettings.json` llamada `PaymentSettings` con un booleano `SimulateFailure`. ¿Cómo inyecto `IConfiguration` en `PaymentService` para leer esa propiedad y forzar un rechazo controlado cuando esté en `true`?"
