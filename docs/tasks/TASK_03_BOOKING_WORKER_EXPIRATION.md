# 📋 Especificación Técnica: Tarea 03
**Rama Git:** `feature/03-booking-worker-expiration`  
**Módulo:** `src/Services/Booking.Api/`  
**Puerto:** N/A (Background Services)  

## 1. Alcance y Responsabilidad
Implementar los procesos en segundo plano para procesar la cola de mensajes y liberar reservas vencidas automáticamente.

## 2. Requisitos de Cátedra
- **Componente:** `IHostedService` / `BackgroundService` en ASP.NET Core.
- **Ciclo de Vida:** Paso 8 de la consigna (Vencimiento de retención).

## 3. Contratos y Endpoints
- Consumo de mensajes: `ReservationRequestedEvent` desde la cola de Azurite/Broker.

## 4. Tareas Concretas
1. Implementar `BookingQueueConsumer`: escucha la cola y deriva al servicio transaccional.
2. Implementar `ReservationExpirationWorker`: timer periódico (cada 15-30s) que busca reservas `Retenida` expiradas y las marca como `Vencida`.
3. Asegurar liberación del asiento al vencerse la reserva.

## 5. Criterios de Aceptación (DoD)
- [ ] El consumidor procesa los mensajes del broker de manera asíncrona.
- [ ] Las reservas con tiempo de retención agotado pasan a `Vencida` automáticamente.

## 6. Defensa Oral
- **¿Por qué el vencimiento se maneja en un BackgroundService?**  
  *Respuesta:* Para desacoplar la liberación de recursos del flujo HTTP del usuario y asegurar limpieza periódica autónoma.
