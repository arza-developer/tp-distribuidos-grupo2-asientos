# 📋 Especificación Técnica: Tarea 02
**Rama Git:** `feature/02-booking-core-concurrency`  
**Módulo:** `src/Services/Booking.Api/`  
**Puerto:** `HTTP 5102`  

## 1. Alcance y Responsabilidad
Implementar el modelo relacional de reservas y garantizar la exclusión mutua para evitar doble reserva (overbooking) ante múltiples réplicas concurrentes.

## 2. Requisitos de Cátedra
- **Patrón:** N-Layer (Presentation, Business, Data Access, Requisito 3.b).
- **Dominio:** Dominio Anémico (Requisito 3.b.ii).
- **Concurrencia:** Exclusión mutua ante múltiples réplicas (Paso 4 de la consigna).

## 3. Contratos y Endpoints
- `GET /api/bookings/{id}`: Consulta interna de estado de reserva.
- Modelo de BD: Entidades `Reservation` y `Seat` con estados (`Retenida`, `Confirmada`, `Rechazada`, `Vencida`).

## 4. Tareas Concretas
1. Configurar Entity Framework Core con SQLite (`booking.db`).
2. Implementar índice único compuesto: `UNIQUE(EventId, SeatNumber, StatusActive)`.
3. Crear servicio de aplicación que intente registrar la reserva: si colisiona por concurrencia, cambia a `Rechazada (Motivo: asiento ocupado)`.

## 5. Criterios de Aceptación (DoD)
- [ ] Ante 10 solicitudes concurrentes sobre el mismo asiento, solo 1 queda `Retenida` y 9 son `Rechazadas`.
- [ ] No se producen excepciones no controladas de bloqueo de base de datos.

## 6. Defensa Oral
- **¿Cómo resuelven la condición de carrera entre réplicas?**  
  *Respuesta:* Mediante restricciones de unicidad a nivel motor relacional y transacciones atómicas, delegando la exclusión mutua a la BD.
