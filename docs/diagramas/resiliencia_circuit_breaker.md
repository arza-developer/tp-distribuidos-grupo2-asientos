# Diagrama de Resiliencia: Pipeline Polly v8 y Circuit Breaker
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA 
**Requisitos:** 3.c, 3.j (Polly v8 en Booking.Api sobre cliente gRPC)

```mermaid
%%{init: {'theme': 'neutral'}}%%
sequenceDiagram
    autonumber
    participant Booking as Booking Core (BookingService)
    participant Polly as Pipeline Polly v8 (HttpResilience)
    participant Payment as Payment gRPC (:5103)

    Note over Polly: Estado inicial: CLOSED<br/>Parámetros: Retry x2 (200ms + Jitter), CB: 50% fallas en 5 llamadas (Break 15s), Timeout 2s

    Booking->>Polly: Invocación ExecuteAsync(ProcessPayment)
    Polly->>Payment: Intento 1 (gRPC HTTP/2)
    Payment-->>Polly: 200 OK
    Polly-->>Booking: Resultado exitoso (Circuito permanece CLOSED)

    Note over Payment: Corte de servicio (contenedor detenido)
    Booking->>Polly: Invocación ExecuteAsync(ProcessPayment)
    Polly->>Payment: Intento 1
    Payment--XPolly: Connection Refused / Timeout
    Note over Polly: Retry: espera 200ms con jitter exponencial
    Polly->>Payment: Intento 2
    Payment--XPolly: Connection Refused

    Note over Polly: Umbral alcanzado (50% fallas sobre >= 5 llamadas): Estado pasa a OPEN (15s)

    Booking->>Polly: Siguiente invocación durante corte
    Polly-->>Booking: BrokenCircuitException inmediato (Fail-Fast sin tráfico de red)
    Booking->>Booking: Captura excepción y eleva PaymentUnavailableException
    Note over Booking: BookingQueueConsumer ejecuta AbandonMessageAsync;<br/>reserva permanece Retained hasta reintento o vencimiento TTL
```
