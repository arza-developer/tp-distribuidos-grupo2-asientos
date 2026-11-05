# Historial de Prompts - Juan Ignacio Vizconti
**Rol:** Tarea 02 - Booking Core (Lógica de Estados y Concurrencia SQLite)  
**Rama:** `feature/02-booking-core-concurrency`

### Contexto y Desafíos Reales
Implementación de la máquina de estados de `Reservation` y resolución de concurrencia sobre el mismo asiento físico.

### Prompts Clave Utilizados

#### Prompt 1: Manejo de Concurrencia en SQLite sin Bloqueos en Memoria
> "Tengo que evitar que dos personas reserven el mismo asiento al mismo tiempo en Booking. Estamos usando SQLite y vamos a tener réplicas del servicio. ¿Cómo configuro en Entity Framework Core un índice único compuesto por EventId y SeatNumber en OnModelCreating, y cómo capturo la excepción específica de SQLite cuando salte el conflicto para traducirla a un rechazo de negocio?"

#### Prompt 2: Transiciones Atómicas en BookingService
> "Necesito implementar los métodos `TryRetainReservationAsync`, `ConfirmReservationAsync` y `RejectReservationAsync`. Si la base me tira `ReservationUniqueConflictException`, tengo que verificar si la reserva ya existía para devolverla o rechazarla con motivo SeatOccupied. Armame la lógica asegurando que no queden estados inconsistentes."

#### Prompt 3: Fix del Bug de Mapeo de Estados (Retained vs. Retenida)
> "En el chat del grupo me pasaron este error en tiempo de ejecución: `BookingUnavailableException: Booking devolvió un estado no reconocido: 'Retained'`. Resulta que Availability espera los enums en español ('Retenida', 'Confirmada', 'Rechazada'). ¿Cómo ajusto el DTO de respuesta en `BookingsController.GetBookingStatus` para que devuelva los strings exactos en español que espera el cliente sin romper el enum interno en C#?"
