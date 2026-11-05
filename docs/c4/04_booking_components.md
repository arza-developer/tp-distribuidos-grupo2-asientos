# C4 Model - Nivel 3: Componentes de Booking (N-Layer)
**Patrón:** N-Layer clásico (Presentation $\to$ Business $\to$ DataAccess) con Dominio Anémico.

## Estructura de Capas
1. **Presentation:** Controladores REST y Workers en segundo plano (`HostedServices`).
2. **Business:** Lógica transaccional de máquina de estados (`BookingService`).
3. **DataAccess:** Repositorio, contexto EF Core SQLite y cliente gRPC hacia Payment protegido con Polly.

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
    subgraph Presentation["Capa de Presentación (Presentation / Workers)"]
        Ctrl["BookingsController\n[REST Endpoints :5102]"]
        QueueWorker["BookingQueueConsumer\n[BackgroundService: Lee topic Service Bus]"]
        ExpireWorker["ReservationExpirationWorker\n[BackgroundService: TTL Check cada 30s]"]
    end

    subgraph Business["Capa de Negocio (Business Logic Layer)"]
        Svc["BookingService\n(Máquina de Estados: Retenida, Confirmada, Rechazada, Vencida)"]
        DTOs["DTOs y Modelos de Entrada/Salida"]
        Svc --- DTOs
    end

    subgraph DataAccess["Capa de Acceso a Datos (Data Access Layer)"]
        Repo["BookingRepository\n[Control de concurrencia y locking]"]
        Ctx["BookingDbContext\n[EF Core SQLite]"]
        GrpcClient["PaymentClient\n[Cliente gRPC con Retry y Circuit Breaker Polly]"]
        AuthM2M["AuthenticationClient\n[Token M2M de servicio]"]
    end

    QueueWorker -->|"Despacha mensaje"| Svc
    ExpireWorker -->|"Ejecuta barrido TTL"| Svc
    Ctrl -->|"Consulta estado"| Svc
    Svc -->|"Transición de estados"| Repo
    Svc -->|"Cobro sincrónico"| GrpcClient
    Repo --> Ctx

    classDef pres fill:#0284c7,stroke:#0369a1,color:#fff;
    classDef biz fill:#16a34a,stroke:#15803d,color:#fff;
    classDef data fill:#4f46e5,stroke:#3730a3,color:#fff;

    class Ctrl,QueueWorker,ExpireWorker pres;
    class Svc,DTOs biz;
    class Repo,Ctx,GrpcClient,AuthM2M data;
```
