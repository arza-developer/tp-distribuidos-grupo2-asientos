# ADR-004: Control de Concurrencia y Exclusión Mutua sobre Asientos

**Estado:** Aprobado  
**Fecha:** 2026-10-26  

## 1. Contexto y Restricciones
- **Paso 4 Consigna:** Múltiples solicitudes concurrentes sobre el mismo asiento deben resolverse determinísticamente: una reserva retenida es creada y el resto debe ser rechazada con motivo `asiento ocupado` (`SeatOccupied`).
- **Entorno:** Múltiples réplicas de `Booking` consumiendo en paralelo del broker de mensajería sobre SQLite.

## 2. Alternativas Evaluadas

### Opción A: Mecanismos de Exclusión Mutua en Memoria (lock, Monitor, SemaphoreSlim)
- **Ventajas:** Cero sobrecarga de red o base de datos.
- **Desventajas:** Solo funcionan dentro de un único proceso de sistema operativo. Al existir múltiples réplicas de `Booking`, los locks en memoria no sincronizan entre contenedores, permitiendo doble reserva inmediata.

### Opción B: Concurrencia Optimista (RowVersion / Timestamp)
- **Ventajas:** No adquiere locks físicos previos.
- **Desventajas:** Requiere leer primero el registro y verificar la versión al confirmar. Ante contención extrema sobre un mismo asiento, provoca un alto porcentaje de excepciones de concurrencia y reintentos innecesarios en la capa de aplicación.

### Opción C: Locks Distribuidos (Redis Redlock / Apache ZooKeeper)
- **Ventajas:** Coordinación global de exclusión mutua antes de tocar la persistencia.
- **Desventajas:** Introduce dependencias de infraestructura adicionales y penalidad de latencia por múltiples round-trips de red para adquirir y liberar el lock por cada intento de asiento.

### Opción D: Restricción de Unicidad en Base de Datos (Seleccionada)
- **Ventajas:** El motor de base de datos garantiza la exclusión mutua atómica en el índice B-Tree. No requiere infraestructura distribuida adicional.
- **Desventajas:** La colisión se manifiesta como excepción de persistencia (`ReservationUniqueConflictException`), requiriendo captura y traducción a lógica de negocio.

## 3. Decisión
Se implementa un **Índice Único Compuesto** en `booking.db` sobre las columnas `(EventId, SeatNumber)` para reservas activas (`Retained`, `Confirmed`).
- La réplica que primero confirma la inserción retiene el asiento.
- Las réplicas competidoras capturan la excepción de unicidad en `BookingService.cs`, transformándola determinísticamente en una reserva rechazada con motivo `ReservationRejectionReason.SeatOccupied (1)`.

## 4. Costo de Mantenimiento y Mitigaciones
- **Sobrecarga de Excepciones:** Manejar colisiones vía excepciones en el ORM tiene costo de CPU.  
  *Mitigación:* Se ejecuta una lectura previa no bloqueante (`HasActiveReservationAsync`) antes del intento de inserción para resolver colisiones evidentes sin disparar la excepción.
