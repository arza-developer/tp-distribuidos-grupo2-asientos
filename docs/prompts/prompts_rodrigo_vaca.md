# Historial de Prompts - Rodrigo Vaca
**Rol:** Tarea 07 - Notifications Worker y Consumo Asíncrono  
**Rama:** `feature/07-notifications-worker`

### Contexto y Desafíos Reales
Consumo de eventos de confirmación en segundo plano y desacoplamiento absoluto de la pasarela de pagos.

### Prompts Clave Utilizados

#### Prompt 1: BackgroundService Autónomo en Docker
> "Tengo que hacer la Tarea 07: un worker de notificaciones que corra en su propio contenedor Docker. ¿Cómo implemento un `BackgroundService` en .NET 8 que mantenga el proceso vivo escuchando eventos sin bloquear el hilo principal y que soporte gracefully shutdown con CancellationToken?"

#### Prompt 2: Inyección de CorrelationId en Serilog LogContext
> "Para cumplir con el Requisito 3.f de auditoría, cada vez que el worker procesa una notificación de reserva confirmada tiene que loguear con Serilog incluyendo el mismo CorrelationId que venía en la solicitud original. ¿Cómo extraigo ese ID de las propiedades del evento y uso `LogContext.PushProperty` para que salga en el log de consola?"

#### Prompt 3: Aislamiento ante Caídas del Canal de Mensajería
> "Discutimos en el grupo qué pasa si el servicio de notificaciones se cae o tira error al mandar un mail simulado. ¿Cómo aseguro a nivel arquitectura que una falla en mi worker jamás haga rollback ni afecte la reserva confirmada en Booking?"
