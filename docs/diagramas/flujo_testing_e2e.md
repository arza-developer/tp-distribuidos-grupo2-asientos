# Diagrama de Secuencia: Flujo de Testing E2E
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA  
**Requisito:** 3.j (Ejecución automatizada de Happy Path y Fallo de Componente)  
**Script:** `scripts/test-e2e-scenarios.sh`

```mermaid
%%{init: {'theme': 'base', 'themeVariables': { 'primaryTextColor': '#f8fafc', 'primaryColor': '#1e293b', 'primaryBorderColor': '#38bdf8', 'lineColor': '#94a3b8', 'edgeLabelBackground': '#1e293b', 'tertiaryColor': '#0f172a', 'fontSize': '13px' }}}%%
sequenceDiagram
    autonumber
    actor Runner as Test Runner (Bash)
    participant Auth as AuthServer (:5104)
    participant Avail as Availability (:5101)
    participant Broker as Azure Service Bus Cloud<br/>(Topic: grupo2-booking-topic)
    participant Booking as Booking API & Worker (:5102)
    participant Payment as Payment gRPC (:5103)
    participant Notif as Notifications Worker

    %% ESCENARIO 1: HAPPY PATH
    rect rgb(30, 41, 59)
    Note over Runner,Notif: Escenario 1: Flujo estandar (Happy Path)
    Runner->>Auth: POST /connect/token (grant_type=password)
    Auth-->>Runner: 200 OK (JWT Bearer Token)

    Runner->>Avail: GET /api/seats (Consulta catalogo publico)
    Avail-->>Runner: 200 OK (Asiento A-1 libre)

    Runner->>Avail: POST /api/reservations (Asiento A-1, Bearer JWT)
    Avail->>Avail: Registra solicitud local (Pending) y bloquea asiento
    Avail->>Broker: Publica ReservationRequestedEvent
    Avail-->>Runner: 202 Accepted (Location: /api/reservations/{id})

    Broker->>Booking: Entrega mensaje (Peek-Lock, suscripcion 'booking')
    Booking->>Booking: TryRetainReservationAsync (Inserta Retained en booking.db)

    Booking->>Auth: POST /connect/token (grant_type=client_credentials)
    Auth-->>Booking: 200 OK (Token M2M)

    Booking->>Payment: gRPC ProcessPayment (HTTP/2, Bearer M2M)
    Payment-->>Booking: PaymentResponse (IsSuccess=true, TxId=1)

    Booking->>Booking: ConfirmReservationAsync (Status = Confirmed en booking.db)
    Booking->>Broker: CompleteMessageAsync (Elimina mensaje del broker)
    Booking-)Notif: ReservationConfirmed (CorrelationId propagado)
    Notif->>Notif: Registra auditoria en log

    loop Sondeo con backoff progresivo
        Runner->>Avail: GET /api/reservations/{id}
        Avail->>Booking: GET /api/bookings/{id} (Token M2M)
        Booking-->>Avail: 200 OK (Status: Confirmed)
        Avail-->>Runner: 200 OK (Status: Confirmed)
    end
    Note over Runner: Criterio: reserva confirmada tras cobro
    end

    %% ESCENARIO 2: CHAOS PATH
    rect rgb(69, 10, 10)
    Note over Runner,Payment: Escenario 2: Falla del servicio de pagos (Chaos Path)
    Runner->>Payment: docker stop seats-payment-grpc
    Note over Payment: Proceso detenido (puerto 5103 no disponible)

    Runner->>Avail: GET /api/seats
    Avail-->>Runner: 200 OK (Catalogo publico operativo)

    Runner->>Avail: POST /api/reservations (Asiento A-2, Bearer JWT)
    Avail->>Broker: Publica ReservationRequestedEvent
    Avail-->>Runner: 202 Accepted (Status: Pending)

    Broker->>Booking: Entrega mensaje (Peek-Lock)
    Booking->>Booking: TryRetainReservationAsync (Inserta Retained en booking.db)

    Booking->>Payment: gRPC ProcessPayment
    Payment--XBooking: Error de conexion (SocketException / Unavailable)

    Note over Booking: Pipeline Polly: Retry (x2 con jitter) y Circuit Breaker
    Booking->>Booking: Captura PaymentUnavailableException
    Booking->>Broker: AbandonMessageAsync (Devuelve mensaje a la cola)
    Note over Booking: Estado permanece en Retained en booking.db

    loop Verificacion de estado
        Runner->>Avail: GET /api/reservations/{id}
        Avail->>Booking: GET /api/bookings/{id} (Token M2M)
        Booking-->>Avail: 200 OK (Status: Retained)
        Avail-->>Runner: 200 OK (Status: Retained)
    end
    Note over Runner: Criterio: la reserva no se confirma sin cobro

    Runner->>Payment: docker start seats-payment-grpc
    Note over Runner: Criterio: servicio de pagos restablecido
    end
```
