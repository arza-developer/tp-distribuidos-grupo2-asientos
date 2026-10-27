
# 🎟️ Sistema Distribuido de Reserva de Asientos - UTN FRBA (Grupo 2)

Trabajo Práctico Integrador de Arquitectura de Software y Sistemas Distribuidos.

## 📌 Asignación de Tareas y Ramas de Trabajo (10 Integrantes)

Cada integrante del equipo trabaja en su rama asignada con especificaciones 100% desacopladas:

|   #    | Rama Git                               | Módulo Principal                            | Especificación Técnica                                          |
|:------:|:---------------------------------------|:--------------------------------------------|:----------------------------------------------------------------|
| **01** | `feature/01-availability-clean-arch`   | `Availability.Api` (Clean Architecture)     | [Ver Tarea 01](docs/tasks/TASK_01_AVAILABILITY_CLEAN_ARCH.md)   |
| **02** | `feature/02-booking-core-concurrency`  | `Booking.Api` (Concurrencia y Datos)        | [Ver Tarea 02](docs/tasks/TASK_02_BOOKING_CORE_CONCURRENCY.md)  |
| **03** | `feature/03-booking-worker-expiration` | `Booking.Api` (Worker y Vencimiento)        | [Ver Tarea 03](docs/tasks/TASK_03_BOOKING_WORKER_EXPIRATION.md) |
| **04** | `feature/04-payment-grpc-service`      | `Payment.Grpc` (Servidor gRPC)              | [Ver Tarea 04](docs/tasks/TASK_04_PAYMENT_GRPC_SERVICE.md)      |
| **05** | `feature/05-resilience-polly`          | `Booking.Api` (Cliente Polly Resiliente)    | [Ver Tarea 05](docs/tasks/TASK_05_RESILIENCE_POLLY.md)          |
| **06** | `feature/06-auth-server-m2m`           | `AuthServer.Api` (OAuth2 y Tokens M2M)      | [Ver Tarea 06](docs/tasks/TASK_06_AUTH_SERVER_M2M.md)           |
| **07** | `feature/07-notifications-worker`      | `Notifications.Worker` (Consumidor Eventos) | [Ver Tarea 07](docs/tasks/TASK_07_NOTIFICATIONS_WORKER.md)      |
| **08** | `feature/08-e2e-testing-diagrams`      | `tests/` (Pruebas Automatizadas y Caos)     | [Ver Tarea 08](docs/tasks/TASK_08_E2E_TESTING_CHAOS.md)         |
| **09** | `feature/09-observability-tracing`     | `src/Common/Observability` (TraceId y Logs) | [Ver Tarea 09](docs/tasks/TASK_09_OBSERVABILITY_TRACING.md)     |
| **10** | `feature/10-architecture-docs`         | `docs/` (Diagramas C4 y Dossier Entrega)    | [Ver Tarea 10](docs/tasks/TASK_10_ARCHITECTURE_DOCS.md)         |

---

## 🚀 Opciones de Ejecución del Proyecto

### Opción A: Desarrollo con Docker

```bash
docker compose up -d --build
```

### Opción B: Desarrollo Local (Sin Docker para máquinas de bajos recursos)

```bash
dotnet run --project src/Services/Booking.Api
```

---

## 🧪 Pruebas de Contratos (Sin Postman)

Ejecutar la suite completa desde terminal:

```bash
npx httpyac tests/api-tests.http --all
```

---

## 📚 Documentación Institucional

- [Tabla de Interpretación de Componentes (Punto 1 y 3.h)](docs/tabla_componentes.md)
