# Máquina de Estados de la Reserva y Ciclo de Vida Distribuido
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA 
**Modelado:** Alineado a la implementación real de `BookingService.cs` y `ReservationExpirationWorker.cs`.

```mermaid
%%{init: {'theme': 'neutral'}}%%
stateDiagram-v2
    [*] --> Pending : Availability recibe solicitud (Paso 3, 202 Accepted)
    
    Pending --> Retained : Booking consume evento desde Azure Service Bus (Peek-Lock)
    Pending --> Rejected : Asiento ocupado en SQLite (Unique Constraint Error 2067)

    state Retained {
        [*] --> EvaluandoCobro
        EvaluandoCobro --> CobroExitoso : gRPC Payment responde 200 OK
        EvaluandoCobro --> CobroRechazado : gRPC Payment rechaza fondos
        EvaluandoCobro --> FallaPasarela : Payment gRPC Unavailable (Circuit Breaker OPEN)
        FallaPasarela --> ReintentoBroker : AbandonMessageAsync (Mensaje liberado a la cola)
    }

    Retained --> Confirmed : Cobro confirmado (CompleteMessageAsync en Azure Service Bus)
    Retained --> Rejected : Cobro denegado por banco (PaymentFailed = 2)
    Retained --> Expired : TTL 10m superado (Barrido periódico cada 15s por BackgroundService)

    Confirmed --> [*] : Evento ReservationConfirmed -> Notifications.Worker
    Rejected --> [*] : Asiento liberado para nuevos pedidos
    Expired --> [*] : Asiento liberado automáticamente por SQLite
```
