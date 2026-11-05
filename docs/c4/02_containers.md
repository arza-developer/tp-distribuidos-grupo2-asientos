# C4 Model - Nivel 2: Contenedores (Container Diagram)
**Frontera Arquitectónica:** Microservicios con Persistencia Aislada (*Database-per-Service*) y Asincronismo.

## Descripción
Detalla los procesos y almacenes de datos que componen la solución en Docker, destacando la frontera de consistencia eventual entre Availability y Booking.

```mermaid
%%{init: {'theme': 'neutral'}}%%
flowchart TB
    User["Cliente / Navegador Web"]

    subgraph CoreContainers["Contenedores del Sistema"]
        AuthServer["Auth Server\n[ASP.NET Core Web API :5104]\nEmisión de tokens JWT (HS256) M2M y Password Grant."]
        ExtIdP["External IdP\n[ASP.NET Core :5105]\nSimulador de Google/GitHub (OIDC)."]
        AvailApp["Availability Site & API\n[Clean Architecture :5101]\nCatálogo público y recepción asíncrona."]
        AvailDb[("Availability DB\n[SQLite]\nCatálogo local.")]
        Broker["Message Broker\n[Azure Service Bus Cloud]\nTopic 'grupo2-booking-topic' / Sub 'booking'"]
        BookingApp["Booking API & Workers\n[N-Layer :5102]\nNúcleo transaccional de reservas."]
        BookingDb[("Booking DB\n[SQLite]\nÍndice único (EventId, SeatNumber).")]
        PaymentApp["Payment Service\n[N-Layer gRPC :5103]\nPasarela financiera HTTP/2 + Protobuf."]
        PaymentDb[("Payment DB\n[SQLite]\nTabla PaymentTransactions con CorrelationId.")]
        NotifWorker["Notifications Worker\n[Background Service]\nProcesamiento asíncrono simulado."]
    end

    User -->|"1. Consulta / Reserva\n[HTTP :5101]"| AvailApp
    User -.->|"Login federado"| ExtIdP
    ExtIdP --> AuthServer
    AvailApp -->|"Token M2M"| AuthServer
    AvailApp --- AvailDb
    AvailApp -->|"2. POST Reserva (HTTP 202)\nPublica evento"| Broker

    subgraph AP_Boundary["Frontera de Consistencia Eventual (Brewer AP)"]
        Broker -.->|"3. Desencola mensaje asíncrono (Peek-Lock)"| BookingApp
    end

    BookingApp --- BookingDb
    BookingApp -->|"4. Cobro sincrónico gRPC\n[HTTP/2 + Polly]"| PaymentApp
    PaymentApp --- PaymentDb
    BookingApp -.->|"5. Evento confirmación (asíncrono)"| NotifWorker
    NotifWorker -.->|"6. Simula envío de notificación"| User

    classDef web fill:#0284c7,stroke:#0369a1,color:#ffffff;
    classDef srv fill:#16a34a,stroke:#15803d,color:#ffffff;
    classDef grpc fill:#4f46e5,stroke:#3730a3,color:#ffffff;
    classDef db fill:#334155,stroke:#1e293b,color:#ffffff;
    classDef broker fill:#d97706,stroke:#b45309,color:#ffffff;
    classDef auth fill:#7c3aed,stroke:#5b21b6,color:#ffffff;

    class AvailApp web;
    class BookingApp srv;
    class PaymentApp grpc;
    class AvailDb,BookingDb,PaymentDb db;
    class Broker broker;
    class AuthServer,ExtIdP auth;
    class NotifWorker srv;

    style AP_Boundary fill:none,stroke:#d97706,stroke-width:2px,stroke-dasharray: 5 5;
```
