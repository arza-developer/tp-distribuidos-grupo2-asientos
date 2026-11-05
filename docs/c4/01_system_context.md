# C4 Model - Nivel 1: Contexto del Sistema (System Context)
**Sistema:** Plataforma Distribuida de Reserva de Asientos 
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA (Grupo 2) 

## Descripción
Representa la interacción de alto nivel entre los usuarios finales, la frontera del sistema de reservas y los proveedores de identidad externos delegados.

```mermaid
%%{init: {'theme': 'neutral'}}%%
flowchart TB
    subgraph Users["Actores / Usuarios"]
        ClientUser["Cliente / Visitante\n[Persona]\nConsulta disponibilidad y reserva asientos."]
    end

    subgraph SystemBoundary["Frontera del Sistema"]
        SeatSystem["Sistema de Reserva de Asientos\n[Sistema de Software]\nPermite consulta de catálogo, reservas asíncronas y cobro gRPC."]
    end

    subgraph ExternalSystems["Sistemas Externos"]
        IdP["3rd Party Identity Provider\n[Google / GitHub Simulado :5105]\nValida identidad federada de usuarios vía OIDC."]
    end

    ClientUser -->|"Consulta catálogo y solicita reservas\n[HTTP / JSON]"| SeatSystem
    ClientUser -.->|"Inicio de sesión federado\n[HTTP / OIDC]"| IdP
    SeatSystem <-->|"Validación de tokens de identidad\n[OAuth2 / OIDC]"| IdP

    classDef userCls fill:#0284c7,stroke:#0369a1,color:#ffffff;
    classDef sysCls fill:#16a34a,stroke:#15803d,color:#ffffff;
    classDef extCls fill:#7c3aed,stroke:#5b21b6,color:#ffffff;

    class ClientUser userCls;
    class SeatSystem sysCls;
    class IdP extCls;
```
