# 📋 Especificación Técnica: Tarea 07
**Rama Git:** `feature/07-notifications-worker`  
**Módulo:** `src/Services/Notifications.Worker/`  
**Tipo:** Consumidor de Eventos Asíncronos  

## 1. Alcance y Responsabilidad
Implementar el microservicio autónomo en segundo plano que escucha eventos de reservas confirmadas desde el Broker y simula la notificación al usuario (Punto 3.h).

## 2. Requisitos de Cátedra
- **Requisito 3.h:** Servicio de notificaciones que informe al usuario cuando la reserva está confirmada con éxito.
- **Consistencia Eventual:** Demostración práctica de propagación asíncrona de eventos.

## 3. Contratos y Eventos
- Evento consumido: `ReservationConfirmedEvent` desde la cola de Azurite / Broker.

## 4. Tareas Concretas
1. Implementar la lectura continua de la cola mediante `BackgroundService`.
2. Procesar el payload de confirmación (`ReservationId`, `UserId`, `SeatNumber`, `TransactionId`).
3. Registrar en log la notificación emitida:
   `[NOTIFICACIÓN] Reserva {ReservationId} confirmada con éxito para el usuario {UserId}. Asiento: {SeatNumber}`.
4. Asegurar confirmación manual de mensaje procesado (`delete message` en Azure Queue / `BasicAck` en RabbitMQ).

## 5. Criterios de Aceptación (DoD)
- [ ] Al confirmarse un pago en Booking, el worker de notificaciones reacciona de forma autónoma e imprime la confirmación.
- [ ] El fallo o reinicio del worker no bloquea el flujo principal de reservas.

## 6. Defensa Oral
- **¿Por qué este servicio demuestra consistencia eventual?**  
  *Respuesta:* Porque la notificación ocurre desacoplada en el tiempo; el usuario ya recibió su confirmación previa mientras este servicio procesa el aviso en diferido.
