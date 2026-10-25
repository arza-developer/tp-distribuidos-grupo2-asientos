# �� Especificación Técnica: Tarea 05
**Rama Git:** `feature/05-resilience-polly`  
**Módulo:** `src/Services/Booking.Api/`  
**Puerto:** N/A (Cliente gRPC saliente)  

## 1. Alcance y Responsabilidad
Configurar el cliente gRPC en Booking para comunicarse con Payment protegido por políticas de tolerancia a fallos.

## 2. Requisitos de Cátedra
- **Librería:** `Microsoft.Extensions.Http.Resilience` / Polly v8 (Paso 5 y 6 de la consigna).
- **Patrones:** Retry con Jitter y Circuit Breaker (Requisito 3.j).

## 3. Contratos y Endpoints
- Invocación a `PaymentApi.PaymentApiClient` (`http://localhost:5103`).

## 4. Tareas Concretas
1. Configurar canal gRPC resiliente con Polly:
   - Timeout: 2 segundos por intento.
   - Retry: 2 reintentos ante fallos transitorios de red.
   - Circuit Breaker: Apertura tras fallos reiterados para fail-fast.
2. Mapear respuestas a estados de negocio:
   - Éxito -> `Confirmada`.
   - Pago fallido -> `Rechazada (Motivo: pago fallido)`.
   - Timeout/Circuito Abierto -> `Rechazada (Motivo: pago no disponible)`.

## 5. Criterios de Aceptación (DoD)
- [ ] Si Payment está caído (`docker stop seats-payment-grpc`), Booking no se bloquea y marca `Rechazada (Motivo: pago no disponible)`.

## 6. Defensa Oral
- **¿Qué previene el Circuit Breaker?**  
  *Respuesta:* Evita la saturación de hilos y sockets por llamadas a un servicio muerto, permitiendo degradación elegante (*Fail Fast*).
