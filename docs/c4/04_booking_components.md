# C4 Model - Nivel 3: Componentes de Booking (N-Layer)
**Patrón:** N-Layer clásico (Presentation -> Business -> DataAccess) con Dominio Anémico.

## Estructura de Capas
1. **Presentation:** Controladores REST y Workers en segundo plano (`HostedServices`).
2. **Business:** Lógica transaccional de máquina de estados (`BookingService`).
3. **DataAccess:** Repositorio, contexto EF Core SQLite y cliente gRPC hacia Payment protegido con Polly.

```mermaid
%%{init: {'theme': 'neutral'}}%%
flowchart TD
    subgraph Presentation["Capa de Presentación (Presentation / Workers)"]
        Ctrl["BookingsController\n[REST Endpoints :5102]"]
        QueueWorker["BookingQueueConsumer\n[BackgroundService: Lee topic Service Bus]"]
        ExpireWorker["ReservationExpirationWorker\n[BackgroundService: barrido cada 15s, TTL 10m]"]
    end

    subgraph Business["Capa de Negocio (Business Logic Layer)"]
        Svc["BookingService\n(Máquina de Estados: Retained, Confirmed, Rejected, Expired)"]
        DTOs["DTOs y Modelos de Entrada/Salida"]
        Svc --- DTOs
    end

    subgraph DataAccess["Capa de Acceso a Datos (Data Access Layer)"]
        Repo["BookingRepository\n[Captura SQLite 2067 -> SeatOccupied]"]
        Ctx["BookingDbContext\n[EF Core SQLite]"]
        GrpcClient["PaymentClient\n[Cliente gRPC con Retry y Circuit Breaker Polly]"]
        AuthM2M["AuthenticationClient\n[Token M2M con SemaphoreSlim]"]
    end

    QueueWorker -->|"Despacha mensaje"| Svc
    ExpireWorker -->|"Ejecuta barrido de vencidos"| Svc
    Ctrl -->|"Consulta estado"| Svc
    Svc -->|"Transición de estados"| Repo
    Svc -->|"Cobro sincrónico"| GrpcClient
    Repo --> Ctx

    classDef pres fill:#0284c7,stroke:#0369a1,color:#ffffff;
    classDef biz fill:#16a34a,stroke:#15803d,color:#ffffff;
    classDef data fill:#4f46e5,stroke:#3730a3,color:#ffffff;

    class Ctrl,QueueWorker,ExpireWorker pres;
    class Svc,DTOs biz;
    class Repo,Ctx,GrpcClient,AuthM2M data;
```
