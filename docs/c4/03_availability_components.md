# C4 Model - Nivel 3: Componentes de Availability (Clean Architecture)
**Patrón:** Clean Architecture (Onion Architecture) con Dominio Anémico.

## Estructura de Capas
Las dependencias apuntan **estrictamente hacia adentro**:
- **Web / Presentation:** Controladores MVC Razor y API REST.
- **Application:** Casos de uso (`Commands`, `Queries`, contratos de puertos).
- **Domain:** Entidades anémicas (`Seat`, `SeatRequest`).
- **Infrastructure:** Implementación de persistencia EF Core, cliente de Service Bus y tokens M2M.

```mermaid
%%{init: {'theme': 'neutral'}}%%
flowchart TD
    subgraph Presentation["Capa de Presentación (Availability.Web :5101)"]
        MvcCtrl["ReservationController & AccountController\n[MVC / Razor UI]"]
        ApiCtrl["ReservationsController & SeatsController\n[REST API]"]
    end

    subgraph Application["Capa de Aplicación (Availability.Application)"]
        CmdReq["RequestReservationCommandHandler\n[Caso de Uso: Reserva]"]
        QuerySeats["GetSeatCatalogQueryHandler\n[Caso de Uso: Catálogo]"]
        Ports["Contratos de Puertos e Interfaces\n(ISeatRepository, IBookingClient, IMessagePublisher)"]
        CmdReq --> Ports
        QuerySeats --> Ports
    end

    subgraph Domain["Capa de Dominio (Availability.Domain) - Anémico"]
        Entities["Entidades POCO\n(Seat, SeatRequest, ReservationStatus)"]
    end

    subgraph Infrastructure["Capa de Infraestructura (Availability.Infrastructure)"]
        EfRepo["AvailabilityDbContext & SeatRepository\n[EF Core SQLite]"]
        BusPub["ServiceBusPublisher\n[Azure Service Bus SDK]"]
        AuthHandler["BookingAuthHandler\n[M2M Client Credentials]"]
    end

    Presentation --> Application
    Application --> Domain
    Infrastructure --> Application
    Infrastructure --> Domain
    EfRepo -.->|"Implementa"| Ports
    BusPub -.->|"Implementa"| Ports

    classDef pres fill:#0284c7,stroke:#0369a1,color:#ffffff;
    classDef app fill:#7c3aed,stroke:#5b21b6,color:#ffffff;
    classDef dom fill:#ea580c,stroke:#c2410c,color:#ffffff;
    classDef inf fill:#334155,stroke:#1e293b,color:#ffffff;

    class MvcCtrl,ApiCtrl pres;
    class CmdReq,QuerySeats,Ports app;
    class Entities dom;
    class EfRepo,BusPub,AuthHandler inf;
```
