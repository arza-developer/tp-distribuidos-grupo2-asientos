# Historial de Prompts - Martín Velázquez
**Rol:** Tarea 04 - Payment Service (gRPC, HTTP/2 y Simulación de Fallas)  
**Rama:** `feature/04-payment-grpc-service`

### Contexto y Desafíos Reales
Diseño del contrato Protobuf, configuración de Kestrel para HTTP/2 nativo y switch de simulación de fallas para pruebas de caos.

### Prompts Clave Utilizados

#### Prompt 1: Contrato Protobuf y Configuración de Proyecto en .NET 8
> "Tengo que armar un servicio gRPC en .NET 8 llamado PaymentService con un método `ProcessPayment` que reciba ReservationId, UserId, Amount y CorrelationId, y devuelva IsSuccess, TransactionId y RejectionReason. ¿Cómo escribo el archivo `payment.proto` (syntax proto3) y qué paquetes NuGet / configuración en el `.csproj` necesito con `<Protobuf GrpcServices="Server" />`?"

#### Prompt 2: Forzar Protocolo HTTP/2 en Kestrel para Docker
> "Cuando intento levantar el contenedor de Payment en Docker, curl me tira error de protocolo porque Kestrel intenta negociar HTTP/1.1 por defecto. ¿Cómo fuerzo en `Program.cs` que Kestrel escuche exclusivamente con `HttpProtocols.Http2` en el puerto configurado?"

#### Prompt 3: Chaos Switch (SimulateFailure) para Pruebas de Resiliencia
> "Para el Requisito 3.j de la cátedra necesitamos simular caídas de la pasarela de pagos sin tener que apagar el contenedor siempre. Quiero agregar una sección en `appsettings.json` llamada `PaymentSettings` con un booleano `SimulateFailure`. ¿Cómo inyecto `IOptions<PaymentSettings>` en `PaymentService` para que si está en `true` devuelva error RPC `StatusCode.Unavailable` o tire excepción controlada?"
