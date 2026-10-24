# 🎟️ Sistema Distribuido de Reserva de Asientos - UTN FRBA (Grupo 2)

Trabajo Práctico Integrador de Arquitectura de Software y Sistemas Distribuidos.

## 📌 Topología de Red y Puertos Asignados

| Servicio                 | Tipo                              | Puerto | Responsabilidad Principal                                                       |
|:-------------------------|:----------------------------------|:-------|:--------------------------------------------------------------------------------|
| **Availability.Api**     | REST Web API (Clean Architecture) | `5101` | Consulta de asientos y recepción asíncrona de reservas (`202 Accepted`).        |
| **Booking.Api**          | REST Web API (N-Layer)            | `5102` | Núcleo de reservas, control de concurrencia y máquina de estados.               |
| **Payment.Grpc**         | gRPC Service (N-Layer)            | `5103` | Pasarela de pagos por contrato binario Protobuf.                                |
| **AuthServer.Api**       | Minimal Web API                   | `5104` | Emisión de tokens JWT para usuarios y comunicación M2M (`POST /connect/token`). |
| **Notifications.Worker** | Background Worker                 | N/A    | Consumidor de eventos de reservas confirmadas.                                  |

---

## 🚀 Cómo compilar y ejecutar localmente

### 1. Requisitos previos
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 2. Compilación del proyecto completo
```bash
dotnet clean
dotnet build
```

### 3. Ejecutar un servicio individual
Para correr cualquiera de los microservicios de forma independiente:
```bash
dotnet run --project src/Services/Availability.Api
# o
dotnet run --project src/Services/Booking.Api
# o
dotnet run --project src/Services/Payment.Grpc
# o
dotnet run --project src/Services/AuthServer.Api
```

---

## 🌿 Flujo de Trabajo en Git (Para no pisarnos)

1. Clonar el repositorio y posicionarse en la rama `develop`:
   ```bash
   git clone <URL_DEL_REPO>
   git checkout develop
   ```
2. Crear tu rama de trabajo según el módulo asignado:
   ```bash
   git checkout -b feature/0X-nombre-del-modulo
   ```
3. Trabajar únicamente dentro de la carpeta asignada a tu módulo.
4. Para integrar cambios, hacer Push de tu rama y abrir un **Pull Request hacia `develop`**.

---

## 📚 Documentación de Entrega
- [Tabla de Interpretación de Componentes (Punto 1 y 3.h)](docs/tabla_componentes.md)
