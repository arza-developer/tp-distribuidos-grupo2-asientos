# Protocolo de Pruebas de Integración y Caos Distribuido (Requisito 3.j)
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA 
**Trabajo Práctico:** Sistema de Reserva de Asientos - Grupo 2 

---

## 1. Criterios de Validación Rigurosa (Anti-Mock)
Para dar cumplimiento estricto al **Punto 3.j de la consigna** (*"La solución final debe compilar y ejecutar un flujo de Happy path de reserva de asientos y uno de fallo en el sistema si un componente falta"*), las pruebas no utilizan dobles de prueba (*mocks*) en memoria, sino interacción real sobre la red Docker:

1. **C1 (Falla Física de Proceso):** Se suspende físicamente el contenedor de la pasarela de pagos (`docker stop seats-payment-grpc`), provocando fallos determinísticos a nivel socket TCP.
2. **C2 (Consistencia Eventual Bounded):** Se verifica que el frontend responda de inmediato con `HTTP 202 Accepted` y se evalúa el progreso del estado mediante sondeo activo (*polling*) con backoff progresivo y límite de tiempo.
3. **C3 (Integridad Transaccional y Eventual Recovery):** Se asegura la transición determinística de la máquina de estados:
   - Happy Path: `Pending (0) -> Retained (1) -> Confirmed (2)`.
   - Failure Path: Ante caída de pagos, la reserva permanece en `Retained (1)` y el mensaje se libera en Azure Service Bus (`AbandonMessageAsync`) para permitir reintento diferido sin generar cobros indebidos.
4. **C4 (Liberación Autónoma de Asientos):** Si la pasarela de pagos no se recupera, la reserva no confirmada es vencida de forma autónoma a estado `Expired (4)` al cumplirse el TTL de 10 minutos mediante el barrido periódico de `ReservationExpirationWorker` (cada 15s).

---

## 2. Ejecución Automatizada de los Escenarios

### Requisitos Previos (Compilación Limpia)
Para garantizar que se ejecute el código fuente más reciente y evitar capas precompiladas desactualizadas (*cache drift*), **es obligatorio utilizar el flag `--build`**:
```bash
docker compose down -v && docker compose up -d --build
```

### Ejecutar Suite E2E
Desde la raíz del repositorio:
```bash
./scripts/test-e2e-scenarios.sh
```

---

## 3. Matriz de Resultados Esperados

| Escenario | Condición de Entrada | Comportamiento Esperado | Estado Final | Criterio de Éxito |
|:---|:---|:---|:---:|:---|
| **Happy Path** | Stack 100% operativo con Azure Service Bus | HTTP 202 -> Encolado -> gRPC OK -> Confirmación persistida en SQLite | `Confirmed` (2) | Asiento bloqueado y cobrado |
| **Failure Path** | `seats-payment-grpc` detenido físicamente | HTTP 202 -> Booking reintenta vía Polly -> Circuit Breaker -> `AbandonMessageAsync` | `Retained` (1) | Integridad financiera: NO se confirma cobro indebido; expiración diferida por TTL (10m) |
