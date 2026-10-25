# 📋 Especificación Técnica: Tarea 07
**Rama Git:** `feature/07-notifications-tracing`  
**Módulo:** `src/Services/Notifications.Worker/`  
**Puerto:** N/A (Consumidor Asíncrono)  

## 1. Alcance y Responsabilidad
Implementar el consumidor de notificaciones de reservas confirmadas y configurar la propagación del CorrelationId en todo el ecosistema.

## 2. Requisitos de Cátedra
- **Requisito 3.h:** Servicio de notificaciones (Event Consumer).
- **Requisito 3.f:** Trazabilidad de inicio a fin con CorrelationId.

## 3. Contratos y Endpoints
- Evento consumido: `ReservationConfirmedEvent`.

## 4. Tareas Concretas
1. Implementar consumidor que escuche `ReservationConfirmedEvent` desde el broker.
2. Imprimir log de confirmación simulando envío de email/push al usuario.
3. Asegurar que el `CorrelationId` viaje a través de cabeceras HTTP, propiedades del broker y metadatos gRPC.

## 5. Criterios de Aceptación (DoD)
- [ ] Al confirmarse una reserva, el worker imprime en consola la notificación con los datos correctos.
- [ ] Se observa el mismo CorrelationId en los logs de Availability, Booking y Notifications.

## 6. Defensa Oral
- **¿Cómo demuestra la consistencia eventual este servicio?**  
  *Respuesta:* El usuario recibe la confirmación asíncronamente después de que el pago impactó en la BD y viajó por el broker.
