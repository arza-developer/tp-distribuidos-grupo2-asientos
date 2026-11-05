# Diagramas de Arquitectura de Software
**Materia:** Programación IV / Sistemas Distribuidos - UTN FRBA  
**Trabajo Práctico:** Sistema de Reserva de Asientos - Grupo 2  
**Estilo Arquitectónico:** Microservicios  

---

## 1. Diagrama General del Sistema y Despliegue (Requisitos 3.d, 3.g, 3.h)
El sistema adopta un estilo de **Microservicios** con persistencia políglota y bases de datos aisladas (*Database-per-Service*).  
La interacción entre **Availability** y **Booking** se realiza a través de un Message Broker asíncrono (**Azure Service Bus Cloud**, topic `grupo2-booking-topic`), marcando la frontera estricta de **Consistencia Eventual (*Eventual Consistency*)**.

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'fontSize': '13px',
    'fontFamily': 'Segoe UI, Helvetica, sans-serif',
    'textColor': '#f8fafc',
    'lineColor': '#94a3b8',
    'edgeLabelBackground': '#1e293b'
  }
}}%%
flowchart TB
    subgraph External["Clientes Externos"]
        UserBrowser["Navegador Web / Cliente HTTP\n(IP Pública)"]
    end

    subgraph IdentityFederation["Seguridad & Identidad"]
        ExtIdP["3rd Party IdP\n(Google/GitHub Simulado)\n:5105"]
        AuthServer["Authorization Server (OAuth2 / OIDC)\nEmisión JWT (HS256)\n:5104"]
    end

    subgraph AvailabilityBoundary["Microservicio Availability (Clean Architecture)"]
        AvailWeb["Availability Site / API\n(ASP.NET Core / MVC / Razor)\n:5101"]
        AvailDb[("SQL Database\n(Catálogo SQLite)")]
        AvailWeb --- AvailDb
    end

    subgraph AsyncBoundary["FRONTERA DE CONSISTENCIA EVENTUAL (AP - Brewer CAP)"]
        Broker["Message Broker (Azure Service Bus Cloud)\nTopic: grupo2-booking-topic\nSuscripción: booking"]
    end

    subgraph BookingBoundary["Microservicio Booking (N-Layer)"]
        BookingConsumer["Booking Queue Consumer\n(Background Worker)"]
        BookingCore["Booking Business Core\n(Gestión de Estados)"]
        BookingDb[("SQL Database\n(Transaccional SQLite)")]
        BookingApi["Booking REST API\n(:5102)"]
        
        BookingConsumer --> BookingCore
        BookingCore --- BookingDb
        BookingApi --> BookingCore
    end

    subgraph PaymentBoundary["Microservicio Payment (N-Layer)"]
        PaymentGrpc["Payment gRPC Service\n(HTTP/2 + Protobuf)\n:5103"]
        PaymentDb[("SQL Database\n(Ledger SQLite)")]
        PaymentGrpc --- PaymentDb
    end

    subgraph NotificationBoundary["Servicio de Notificaciones (Requisito 3.h)"]
        NotifWorker["Notifications Worker\n(Background Consumer Simulado)"]
    end

    %% Relaciones
    UserBrowser -->|"1. Consulta asientos / Login"| AvailWeb
    UserBrowser -.->|"Autenticación Usuario"| ExtIdP
    ExtIdP --> AuthServer
    AvailWeb -->|"Token M2M"| AuthServer
    AvailWeb -->|"2. POST Reserva (HTTP 202 Accepted)\nEncola mensaje asíncrono"| Broker
    Broker -.->|"3. Desencola solicitud (Peek-Lock)\n[Consistencia Eventual]"| BookingConsumer
    BookingCore -->|"4. Cobro sincrónico gRPC\n(Resiliencia Polly: Retry + Circuit Breaker)"| PaymentGrpc
    
    BookingCore -->|"5. Emite evento de confirmación"| NotifWorker
    NotifWorker -.->|"6. Notifica confirmación al usuario"| UserBrowser
    AvailWeb -->|"Consulta estado (Tracking M2M)"| BookingApi

    %% Paleta de colores para cada Microservicio / Rol
    classDef client fill:#0284c7,stroke:#38bdf8,stroke-width:2px,color:#ffffff;
    classDef auth fill:#7c3aed,stroke:#a855f7,stroke-width:2px,color:#ffffff;
    classDef avail fill:#0d9488,stroke:#14b8a6,stroke-width:2px,color:#ffffff;
    classDef broker fill:#d97706,stroke:#f59e0b,stroke-width:2px,color:#ffffff;
    classDef booking fill:#16a34a,stroke:#22c55e,stroke-width:2px,color:#ffffff;
    classDef payment fill:#4f46e5,stroke:#818cf8,stroke-width:2px,color:#ffffff;
    classDef notif fill:#e11d48,stroke:#fb7185,stroke-width:2px,color:#ffffff;
    classDef db fill:#334155,stroke:#64748b,stroke-width:2px,color:#ffffff;

    class UserBrowser client;
    class ExtIdP,AuthServer auth;
    class AvailWeb avail;
    class Broker broker;
    class BookingConsumer,BookingCore,BookingApi booking;
    class PaymentGrpc payment;
    class NotifWorker notif;
    class AvailDb,BookingDb,PaymentDb db;

    %% Contornos de subsistemas
    style External stroke:#38bdf8,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style IdentityFederation stroke:#a855f7,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style AvailabilityBoundary stroke:#14b8a6,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style AsyncBoundary fill:#1e293b,stroke:#f59e0b,stroke-width:2px,stroke-dasharray: 6 6,color:#fef3c7
    style BookingBoundary stroke:#22c55e,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style PaymentBoundary stroke:#818cf8,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style NotificationBoundary stroke:#fb7185,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
```

> **Nota de Cátedra sobre Consistencia Eventual (Requisito 3.g):**  
> Cuando el cliente solicita reservar un asiento, **Availability** valida la existencia en catálogo y responde **inmediatamente con código HTTP 202 Accepted**, registrando la solicitud localmente y enviando el evento al broker. La confirmación definitiva la ejecuta **Booking** de forma asíncrona; la consistencia entre catálogo y reserva es **eventual**.

---

## 2. Diagrama de Arquitectura: Availability Site / API (Requisito 3.a, 3.i)
Sigue estrictamente el patrón **Clean Architecture (Onion Architecture)** con **Dominio Anémico**:
- Las dependencias apuntan **hacia adentro**.
- `Availability.Domain` no tiene dependencias de infraestructura ni frameworks.
- La persistencia y el cliente de mensajería implementan interfaces definidas en la capa de aplicación.

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'fontSize': '13px',
    'fontFamily': 'Segoe UI, Helvetica, sans-serif',
    'textColor': '#f8fafc',
    'lineColor': '#94a3b8',
    'edgeLabelBackground': '#1e293b'
  }
}}%%
flowchart TD
    subgraph PresentationLayer["Capa de Presentación & Web (Availability.Web)"]
        MvcControllers["MVC Controllers (Account, Home, Reservation)"]
        ApiControllers["REST Controllers (ReservationsController, SeatsController)"]
        RazorViews["Razor Views (UI de Catálogo y Login)"]
        MvcControllers --> RazorViews
    end

    subgraph ApplicationLayer["Capa de Aplicación (Availability.Application)"]
        Commands["Commands / Handlers (RequestReservation, SyncReservationStatus)"]
        Queries["Queries / Handlers (GetSeatCatalog, GetSeats)"]
        PortsIn["Interfaces / Abstracciones (IBookingClient, ISeatRepository)"]
        Commands --> PortsIn
        Queries --> PortsIn
    end

    subgraph DomainLayer["Capa de Dominio (Availability.Domain) - Dominio Anémico"]
        Entities["Entidades POCO (Seat, SeatRequest)"]
        Enums["Enums (ReservationStatus)"]
        Exceptions["Excepciones de Negocio"]
    end

    subgraph InfrastructureLayer["Capa de Infraestructura (Availability.Infrastructure)"]
        EFCore["AvailabilityDbContext (EF Core SQLite)"]
        RepoImpl["Implementación Repositorios (SeatRepository)"]
        BusPublisher["ServiceBusPublisher (Azure Service Bus SDK)"]
        AuthClients["AuthServerLoginClient & BookingAuthHandler"]
    end

    PresentationLayer --> ApplicationLayer
    ApplicationLayer --> DomainLayer
    InfrastructureLayer --> ApplicationLayer
    InfrastructureLayer --> DomainLayer
    RepoImpl -.->|"Implementa"| PortsIn
    BusPublisher -.->|"Implementa"| PortsIn

    %% Colores por Capa de Clean Architecture
    classDef presentation fill:#0284c7,stroke:#38bdf8,stroke-width:2px,color:#ffffff;
    classDef application fill:#7c3aed,stroke:#a855f7,stroke-width:2px,color:#ffffff;
    classDef domain fill:#ea580c,stroke:#fb923c,stroke-width:2px,color:#ffffff;
    classDef infra fill:#334155,stroke:#64748b,stroke-width:2px,color:#ffffff;

    class MvcControllers,ApiControllers,RazorViews presentation;
    class Commands,Queries,PortsIn application;
    class Entities,Enums,Exceptions domain;
    class EFCore,RepoImpl,BusPublisher,AuthClients infra;

    style PresentationLayer stroke:#38bdf8,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style ApplicationLayer stroke:#a855f7,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style DomainLayer stroke:#fb923c,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style InfrastructureLayer stroke:#94a3b8,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
```

---

## 3. Diagrama de Arquitectura: Booking REST API (Requisito 3.b, 3.i)
Implementado bajo el patrón arquitectónico clásico **N-Layer (Multicapa)** con **Dominio Anémico**:

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'fontSize': '13px',
    'fontFamily': 'Segoe UI, Helvetica, sans-serif',
    'textColor': '#f8fafc',
    'lineColor': '#94a3b8',
    'edgeLabelBackground': '#1e293b'
  }
}}%%
flowchart TD
    subgraph Presentation["Capa de Presentación (Presentation / Web API)"]
        BookingsCtrl["BookingsController (REST Endpoints)"]
        QueueWorker["BookingQueueConsumer (BackgroundService)"]
        ExpireWorker["ReservationExpirationWorker (TTL Check cada 15s)"]
    end

    subgraph Business["Capa de Negocio (Business)"]
        IBookingSvc["IBookingService"]
        BookingSvc["BookingService (Máquina de Estados: Retained, Confirmed, Rejected, Expired)"]
        DTOs["DTOs (ReserveSeatRequest, BookingStatusDto)"]
        IBookingSvc --- BookingSvc
    end

    subgraph DataAccess["Capa de Acceso a Datos (DataAccess)"]
        Repo["BookingRepository"]
        Context["BookingDbContext (SQLite)"]
        PaymentCli["PaymentClient (gRPC Client con Polly v8)"]
        AuthCli["AuthenticationClient (M2M Token Handler con SemaphoreSlim)"]
        Entities["Modelos de Datos / Entidades Anémicas (Reservation)"]
    end

    Presentation --> Business
    Business --> DataAccess
    Repo --> Context
    Context --> Entities

    %% Colores N-Layer
    classDef presLayer fill:#0284c7,stroke:#38bdf8,stroke-width:2px,color:#ffffff;
    classDef bizLayer fill:#16a34a,stroke:#22c55e,stroke-width:2px,color:#ffffff;
    classDef dataLayer fill:#4f46e5,stroke:#818cf8,stroke-width:2px,color:#ffffff;

    class BookingsCtrl,QueueWorker,ExpireWorker presLayer;
    class IBookingSvc,BookingSvc,DTOs bizLayer;
    class Repo,Context,PaymentCli,AuthCli,Entities dataLayer;

    style Presentation stroke:#38bdf8,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style Business stroke:#22c55e,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style DataAccess stroke:#818cf8,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
```

---

## 4. Diagrama de Arquitectura: Payment gRPC Service (Requisito 3.c, 3.i)
Diseñado bajo patrón **N-Layer**, exponiendo interfaz de alto rendimiento HTTP/2 con Protobuf:

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'fontSize': '13px',
    'fontFamily': 'Segoe UI, Helvetica, sans-serif',
    'textColor': '#f8fafc',
    'lineColor': '#94a3b8',
    'edgeLabelBackground': '#1e293b'
  }
}}%%
flowchart TD
    subgraph GrpcPresentation["Capa gRPC / Exposición"]
        GrpcProto["Contrato Protobuf (payment.proto)"]
        GrpcService["PaymentService (Implementación del Servicio gRPC)"]
        GrpcProto --> GrpcService
    end

    subgraph GrpcBusiness["Capa de Negocio & Simulación de Fallos"]
        TxValidation["Validación de Transacción Financiera"]
        ChaosSwitch["Simulador de Caídas / Fallos (PaymentSettings:SimulateFailure)"]
        GrpcService --> TxValidation
        TxValidation --> ChaosSwitch
    end

    subgraph GrpcDataAccess["Capa de Datos & Persistencia"]
        PaymentDb["PaymentDbContext (SQLite Ledger)"]
        TxEntity["PaymentTransaction (Registro Inmutable)"]
        ChaosSwitch --> PaymentDb
        PaymentDb --> TxEntity
    end

    %% Colores gRPC N-Layer
    classDef grpcPres fill:#7c3aed,stroke:#a855f7,stroke-width:2px,color:#ffffff;
    classDef grpcBiz fill:#e11d48,stroke:#fb7185,stroke-width:2px,color:#ffffff;
    classDef grpcData fill:#0d9488,stroke:#14b8a6,stroke-width:2px,color:#ffffff;

    class GrpcProto,GrpcService grpcPres;
    class TxValidation,ChaosSwitch grpcBiz;
    class PaymentDb,TxEntity grpcData;

    style GrpcPresentation stroke:#a855f7,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style GrpcBusiness stroke:#fb7185,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
    style GrpcDataAccess stroke:#14b8a6,stroke-width:1.5px,stroke-dasharray: 4 4,fill:none
```

---

## 5. Flujo Dinámico y Gestión de Errores (Happy Path & Fallos)
Muestra el ciclo de vida completo de una solicitud de reserva, incluyendo la validación síncrona en el frontend/API, la frontera asíncrona de consistencia eventual y la resiliencia en el cobro con Polly.

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'fontSize': '12px',
    'fontFamily': 'Segoe UI, Helvetica, sans-serif',
    'primaryTextColor': '#f8fafc',
    'edgeLabelBackground': '#1e293b',
    'lineColor': '#94a3b8'
  }
}}%%
sequenceDiagram
    autonumber
    actor User as Usuario / Browser
    participant Avail as Availability API
    participant Broker as Azure Service Bus Cloud
    participant Booking as Booking Core / Worker
    participant Payment as Payment gRPC

    Note over User,Avail: 1. Validación Sincrónica de Entrada (Availability)
    User->>Avail: POST /api/reservations (EventId, SeatNumber, UserId, Price)
    
    alt Asiento Inexistente (SeatNotFoundException)
        Avail-->>User: HTTP 404 Not Found
    else Asiento Ocupado / No Disponible (SeatUnavailableException)
        Avail-->>User: HTTP 409 Conflict
    else Parámetros Inválidos (BusinessValidationException)
        Avail-->>User: HTTP 400 Bad Request
    else Happy Path: Aceptado para procesamiento
        Avail->>Broker: Encola mensaje (ReservationRequestedEvent)
        Avail-->>User: HTTP 202 Accepted (Location: /api/reservations/{id})
    end

    Note over Broker,Payment: 2. Procesamiento Asíncrono & Consistencia Eventual
    Broker-->>Booking: Desencola mensaje (Peek-Lock en 'booking')
    Booking->>Booking: Inserta reserva (Status: Retained, TTL: 10m)
    Booking->>Payment: gRPC ProcessPayment(TxRequest)
    
    alt Fallo de Saldo / Rechazo Financiero
        Payment-->>Booking: gRPC PaymentResponse (IsSuccess=false)
        Booking->>Booking: Transiciona estado a "Rejected" (PaymentFailed)
    else Caída de Payment (Chaos / Timeout / Broken Circuit)
        Payment--xBooking: gRPC UNAVAILABLE
        Note over Booking: Polly v8: Retry x2 -> Circuit Breaker
        Booking->>Broker: AbandonMessageAsync (Mantiene "Retained" para reintento diferido)
        Note over Booking: Si no se recupera, ExpireWorker la vence tras 10m
    else Happy Path: Cobro Exitoso
        Payment-->>Booking: gRPC PaymentResponse (IsSuccess=true)
        Booking->>Booking: Transiciona estado a "Confirmed"
        Booking->>Broker: CompleteMessageAsync
    end
```

### Matriz de Mapeo: Excepciones de Dominio a Códigos HTTP/gRPC

| Escenario / Excepción | Capa Origen | Código de Respuesta | Efecto en el Sistema |
| :--- | :--- | :--- | :--- |
| **Happy Path (Encolado)** | `Availability.Web` | `HTTP 202 Accepted` | Solicitud aceptada en broker; consistencia eventual en marcha. |
| `BusinessValidationException` | `Availability.Domain` | `HTTP 400 Bad Request` | Falla validación de entrada; no se encola nada. |
| `InvalidReservationRequestException` | `Availability.Domain` | `HTTP 400 Bad Request` | Payload mal formado o faltan campos obligatorios. |
| `SeatNotFoundException` | `Availability.Domain` | `HTTP 404 Not Found` | El asiento solicitado no existe en el catálogo. |
| `SeatUnavailableException` | `Availability.Domain` | `HTTP 409 Conflict` | El asiento ya se encuentra reservado o no disponible. |
| `ReservationUserMismatchException` | `Availability.Domain` | `HTTP 403 Forbidden` | Discrepancia entre el usuario autenticado (token `sub`) y el cuerpo de la solicitud. |
| `Payment Error` | `Payment.gRPC` | `gRPC PaymentResponse (IsSuccess=false)` | Fondos insuficientes; reserva pasa a `Rejected (PaymentFailed)`. |
| `Payment Service Down` | `Booking -> Payment` | `Polly Circuit Breaker / Abandon` | Falla gRPC interceptada; mensaje liberado a la cola para reintento diferido. |
