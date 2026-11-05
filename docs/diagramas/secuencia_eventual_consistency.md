# Diagrama de Secuencia: Consistencia Eventual y Frontera AP
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA  
**Requisitos:** 3.a, 3.b, 3.g (Frontera de Consistencia Eventual y Asincronismo)

```mermaid
%%{init: {'theme': 'base', 'themeVariables': { 'primaryTextColor': '#f8fafc', 'primaryColor': '#1e293b', 'primaryBorderColor': '#38bdf8', 'lineColor': '#94a3b8', 'edgeLabelBackground': '#1e293b', 'tertiaryColor': '#0f172a', 'fontSize': '13px' }}}%%
sequenceDiagram
    autonumber
    actor Cliente as Cliente HTTP
    participant Avail as Availability API (:5101)
    participant Broker as Azure Service Bus Cloud<br/>(Topic: grupo2-booking-topic)
    participant Booking as Booking API & Consumer (:5102)
    participant Payment as Payment gRPC (:5103)
    participant Notif as Notifications Worker

    Cliente->>Avail: POST /api/reservations (Asiento A-1, Bearer JWT)
    Note over Avail: Valida catalogo local, guarda solicitud<br/>y marca asiento no disponible temporalmente
    Avail->>Broker: Publica ReservationRequestedEvent
    Avail-->>Cliente: HTTP 202 Accepted (Location: /api/reservations/{id})
    Note over Cliente: Frontera AP: respuesta inmediata sin esperar cobro

    Broker->>Booking: Entrega mensaje (Peek-Lock en suscripcion 'booking')
    
    critical Procesamiento asincrono en Booking
        Booking->>Booking: Inserta reserva (Status: Retained, TTL: 10m)
        Booking->>Payment: gRPC ProcessPayment(ReservationId, Amount)
    option Pago exitoso
        Payment-->>Booking: PaymentResponse(IsSuccess=true)
        Booking->>Booking: Actualiza estado a Confirmed en booking.db
        Booking->>Broker: CompleteMessageAsync (Confirma recepcion)
        Booking-)Notif: Emite evento ReservationConfirmed
    option Falla de pasarela (gRPC Unavailable / Timeout)
        Payment--XBooking: RpcException / Circuit Breaker abierto
        Booking->>Booking: Captura excepcion, mantiene Retained en base
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
