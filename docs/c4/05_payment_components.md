# C4 Model - Nivel 3: Componentes de Payment (N-Layer gRPC)
**Patrón:** N-Layer con interfaz binaria gRPC sobre HTTP/2.

## Descripción
Expone el contrato de cobro financiero y alberga el interruptor de fallos simulados (*Chaos Switch*) para las pruebas de resiliencia.

```mermaid
%%{init: {'theme': 'neutral'}}%%
flowchart TD
    subgraph Presentation["Capa de Exposición gRPC (HTTP/2)"]
        ProtoContract["payment.proto\n[Contrato estricto Protobuf]"]
        GrpcSvc["PaymentService\n[Servidor gRPC Kestrel :5103]"]
        ProtoContract --> GrpcSvc
    end

    subgraph Business["Capa de Negocio y Simulación"]
        TxVal["Validación Financiera de Transacción"]
        Chaos["Chaos Switch\n[Flag SimulateFailure en appsettings]"]
        GrpcSvc --> TxVal
        TxVal --> Chaos
    end

    subgraph DataAccess["Capa de Datos y Registro"]
        PaymentDb["PaymentDbContext\n[SQLite: payment.db]"]
        TxRec["PaymentTransaction\n[Idempotencia contable por CorrelationId]"]
        Chaos --> PaymentDb
        PaymentDb --> TxRec
    end

    classDef pres fill:#7c3aed,stroke:#5b21b6,color:#ffffff;
    classDef biz fill:#e11d48,stroke:#be123c,color:#ffffff;
    classDef data fill:#0d9488,stroke:#0f766e,color:#ffffff;

    class ProtoContract,GrpcSvc pres;
    class TxVal,Chaos biz;
    class PaymentDb,TxRec data;
```
