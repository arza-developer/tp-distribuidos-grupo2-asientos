# C4 Model - Nivel 1: Contexto del Sistema (System Context)
**Sistema:** Plataforma Distribuida de Reserva de Asientos  
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA (Grupo 2)  

## Descripción
Representa la interacción de alto nivel entre los usuarios finales, la frontera del sistema de reservas y los proveedores de identidad externos delegados.

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
    subgraph Users["Actores / Usuarios"]
        ClientUser["👤 Cliente / Visitante\n[Persona]\nConsulta disponibilidad y reserva asientos."]
    end

    subgraph SystemBoundary["Frontera del Sistema"]
        SeatSystem["🏢 Sistema de Reserva de Asientos\n[Sistema de Software]\nPermite consulta pública, emisión de reservas, cobro financiero y notificaciones."]
    end

    subgraph ExternalSystems["Sistemas Externos"]
        IdP["🔑 3rd Party Identity Provider\n[Google / GitHub Simulado]\nValida identidad federada de usuarios."]
    end

    ClientUser -->|"Consulta catálogo, solicita asientos y sigue el estado\n[HTTPS / JSON]"| SeatSystem
    ClientUser -.->|"Inicia sesión federada\n[HTTPS / OIDC]"| IdP
    SeatSystem <-->|"Valida tokens de identidad\n[OAuth2 / OIDC]"| IdP

    classDef userCls fill:#0284c7,stroke:#0369a1,color:#fff,stroke-width:2px;
    classDef sysCls fill:#16a34a,stroke:#15803d,color:#fff,stroke-width:2px;
    classDef extCls fill:#7c3aed,stroke:#5b21b6,color:#fff,stroke-width:2px;

    class ClientUser userCls;
    class SeatSystem sysCls;
    class IdP extCls;
```
