# Historial de Prompts - Micaela Andini
**Rol:** Tarea 05 - Resiliencia con Polly en Booking y Organización N-Layer  
**Rama:** `feature/05-resilience-polly`

### Contexto y Desafíos Reales
Configuración de políticas de tolerancia a fallos en el cliente gRPC de Booking y refactorización del proyecto a capas limpias N-Layer.

### Prompts Clave Utilizados

#### Prompt 1: Migración a Polly v8 con AddResilienceHandler
> "En la cátedra vimos ejemplos con el Polly viejo (`Policy.Handle().CircuitBreakerAsync()`), pero en .NET 8 con `AddGrpcClient` se usa `AddResilienceHandler` de `Microsoft.Extensions.Http.Resilience`. ¿Cómo configuro un pipeline que tenga Retry exponencial con Jitter (2 intentos, 200ms) y un Circuit Breaker con FailureRatio 0.5 y muestreo de 30 segundos?"

#### Prompt 2: Umbral de MinimumThroughput en Circuit Breaker
> "Estoy probando el Circuit Breaker localmente tirando 2 peticiones con Payment apagado, pero el circuito sigue en CLOSED y no pasa a OPEN. ¿Por qué pasa esto? Ah, tiene que ver con `MinimumThroughput`? Explicame cómo setear peticiones mínimas y ventana para que en las pruebas locales se abra rápido ante fallas."

#### Prompt 3: Degradación Elegante y Liberación de Asientos
> "Cuando el Circuit Breaker se abre y tira `BrokenCircuitException`, Booking no puede devolver error 500 al cliente. Tiene que capturar la excepción en `PaymentClient`, marcar la reserva como Rechazada con motivo `PaymentUnavailable` y llamar al repositorio para liberar el asiento. ¿Cómo estructuro ese bloque try/catch para que no se escapen excepciones de Polly?"
