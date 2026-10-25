# 📋 Especificación Técnica: Tarea 01
**Rama Git:** `feature/01-availability-clean-arch`  
**Módulo:** `src/Services/Availability.Api/`  
**Puerto:** `HTTP 5101`  

## 1. Alcance y Responsabilidad
Implementar la fachada pública de catálogo y recepción de reservas bajo arquitectura limpia, aislando la lógica de casos de uso de la infraestructura.

## 2. Requisitos de Cátedra
- **Patrón:** Clean Architecture (Robert C. Martin).
- **Dominio:** Dominio Anémico (Entidades POCO sin lógica de negocio, Requisito 3.a.ii).
- **Consistencia:** Eventual Consistency (Punto 3.g, respuesta 202 Accepted inmediata).

## 3. Contratos y Endpoints
- `GET /api/seats`: Catálogo público de asientos libres/ocupados.
- `POST /api/reservations`: Recepción asíncrona (publica `ReservationRequestedEvent` y devuelve 202).
- `GET /api/reservations/{id}`: Proxy de consulta de estado hacia Booking.

## 4. Tareas Concretas
1. Reestructurar el proyecto en capas internas: `Domain`, `UseCases`, `Adapters` e `Infrastructure`.
2. Crear la entidad anémica `Seat` y el caso de uso `RequestReservationUseCase`.
3. Inyectar `IMessagePublisher` en el caso de uso para desacoplar el broker.
4. Registrar log con formato `[{CorrelationId}] INICIO: ...` (Requisito 3.f).

## 5. Criterios de Aceptación (DoD)
- [ ] Compila con `dotnet build` sin dependencias de infraestructura en el dominio.
- [ ] `POST /api/reservations` devuelve HTTP 202 con `reservationId`.

## 6. Defensa Oral
- **¿Por qué las entidades son anémicas?**  
  *Respuesta:* Requisito explícito 3.a.ii; la orquestación reside en los Interactors y las entidades solo contienen estado.
