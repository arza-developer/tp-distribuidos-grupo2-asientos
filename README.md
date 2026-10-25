# 🎟️ Sistema Distribuido de Reserva de Asientos - UTN FRBA (Grupo 2)

Trabajo Práctico Integrador de Arquitectura de Software y Sistemas Distribuidos.

## 📌 Asignación de Tareas y Ramas de Trabajo

Cada integrante trabaja en su rama asignada siguiendo su especificación técnica estandarizada:

|   #    | Rama Git                               | Módulo Principal                     | Especificación Detallada                                        |
|:------:|:---------------------------------------|:-------------------------------------|:----------------------------------------------------------------|
| **01** | `feature/01-availability-clean-arch`   | `src/Services/Availability.Api/`     | [Ver Tarea 01](docs/tasks/TASK_01_AVAILABILITY_CLEAN_ARCH.md)   |
| **02** | `feature/02-booking-core-concurrency`  | `src/Services/Booking.Api/`          | [Ver Tarea 02](docs/tasks/TASK_02_BOOKING_CORE_CONCURRENCY.md)  |
| **03** | `feature/03-booking-worker-expiration` | `src/Services/Booking.Api/`          | [Ver Tarea 03](docs/tasks/TASK_03_BOOKING_WORKER_EXPIRATION.md) |
| **04** | `feature/04-payment-grpc-service`      | `src/Services/Payment.Grpc/`         | [Ver Tarea 04](docs/tasks/TASK_04_PAYMENT_GRPC_SERVICE.md)      |
| **05** | `feature/05-resilience-polly`          | `src/Services/Booking.Api/`          | [Ver Tarea 05](docs/tasks/TASK_05_RESILIENCE_POLLY.md)          |
| **06** | `feature/06-auth-server-m2m`           | `src/Services/AuthServer.Api/`       | [Ver Tarea 06](docs/tasks/TASK_06_AUTH_SERVER_M2M.md)           |
| **07** | `feature/07-notifications-tracing`     | `src/Services/Notifications.Worker/` | [Ver Tarea 07](docs/tasks/TASK_07_NOTIFICATIONS_TRACING.md)     |
| **08** | `feature/08-e2e-testing-diagrams`      | `tests/` y `docs/`                   | [Ver Tarea 08](docs/tasks/TASK_08_E2E_TESTING_DIAGRAMS.md)      |

---

## 🚀 Opciones de Ejecución del Proyecto

### Opción A: Desarrollo con Docker
```bash
docker compose up -d --build
```

### Opción B: Desarrollo Local (Sin Docker)
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
