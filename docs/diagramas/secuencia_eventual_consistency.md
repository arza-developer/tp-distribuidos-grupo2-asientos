# Diagrama de Secuencia: Consistencia Eventual y Frontera AP
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA 
**Requisitos:** 3.a, 3.b, 3.g (Frontera de Consistencia Eventual y Asincronismo)

```mermaid
%%{init: {'theme': 'neutral'}}%%
sequenceDiagram
    autonumber
    actor Cliente as Cliente HTTP
    participant Avail as Availability API (:5101)
    participant Broker as Azure Service Bus Cloud<br/>(Topic: grupo2-booking-topic)
    participant Booking as Booking API & Consumer (:5102)
    participant Payment as Payment gRPC (:5103)
    participant Notif as Notifications Worker

    Cliente->>Avail: POST /api/reservations (Asiento A-1, Bearer JWT)
    Note over Avail: Valida catálogo local, guarda solicitud<br/>y marca asiento no disponible temporalmente
    Avail->>Broker: Publica ReservationRequestedEvent
    Avail-->>Cliente: HTTP 202 Accepted (Location: /api/reservations/{id})
    Note over Cliente: Frontera AP: respuesta inmediata sin esperar cobro

    Broker->>Booking: Entrega mensaje (Peek-Lock en suscripción 'booking')
    Booking->>Booking: Inserta reserva (Status: Retained, TTL: 10m)
    Booking->>Payment: gRPC ProcessPayment(ReservationId, Amount)

    alt Pago exitoso
        Payment-->>Booking: PaymentResponse(IsSuccess=true)
        Booking->>Booking: Actualiza estado a Confirmed en booking.db
        Booking->>Broker: CompleteMessageAsync (Confirma recepción)
        Booking-)Notif: Emite evento ReservationConfirmed
    else Falla de pasarela (gRPC Unavailable / Timeout)
        Payment--XBooking: RpcException / Circuit Breaker abierto
        Booking->>Booking: Captura excepción, mantiene Retained en base
        Booking->>Broker: AbandonMessageAsync (Libera mensaje para reintento)
        Note over Booking: Si no se recupera, ReservationExpirationWorker<br/>vence la reserva tras 10m (barrido cada 15s)
    end

    loop Sondeo del cliente
        Cliente->>Avail: GET /api/reservations/{id}
        Avail->>Booking: GET /api/bookings/{id} (Token M2M)
        Booking-->>Avail: 200 OK (Status: Confirmed / Retained)
        Avail-->>Cliente: 200 OK (Estado actual)
    end
```
