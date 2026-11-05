# C4 Model - Nivel 3: Componentes de Payment (N-Layer gRPC)
**Patrón:** N-Layer con interfaz binaria gRPC sobre HTTP/2.

## Descripción
Expone el contrato de cobro financiero y alberga el interruptor de fallos simulados (*Chaos Switch*) para las pruebas de resiliencia.

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
    subgraph Presentation["Capa de Exposición gRPC (HTTP/2)"]
        ProtoContract["payment.proto\n[Contrato estricto Protobuf]"]
        GrpcSvc["PaymentService\n[Servidor gRPC Kestrel :5103]"]
        ProtoContract --> GrpcSvc
    end

    subgraph Business["Capa de Negocio y Simulación"]
        TxVal["Validación Financiera de Transacción"]
        Chaos["Chaos Switch\n(Simulador de caída de red / latencia)"]
        GrpcSvc --> TxVal
        TxVal --> Chaos
    end

    subgraph DataAccess["Capa de Datos y Registro Contable"]
        Ledger["PaymentDb / InMemory Ledger"]
        TxRec["PaymentTransaction\n[Registro Inmutable de Cobro]"]
        Chaos --> Ledger
        Ledger --> TxRec
    end

    classDef pres fill:#7c3aed,stroke:#5b21b6,color:#fff;
    classDef biz fill:#e11d48,stroke:#be123c,color:#fff;
    classDef data fill:#0d9488,stroke:#0f766e,color:#fff;

    class ProtoContract,GrpcSvc pres;
    class TxVal,Chaos biz;
    class Ledger,TxRec data;
```
