# Protocolo de Pruebas de Integración y Caos Distribuido (Requisito 3.j)
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA  
**Trabajo Práctico:** Sistema de Reserva de Asientos - Grupo 2  

---

## 1. Criterios de Validación Rigurosa (Anti-Mock)
Para dar cumplimiento estricto al **Punto 3.j de la consigna** (*"La solución final debe compilar y ejecutar un flujo de Happy path de reserva de asientos y uno de fallo en el sistema si un componente falta"*), las pruebas no utilizan dobles de prueba (*mocks*) en memoria, sino interacción real sobre la red Docker:

1. **C1 (Falla Física de Proceso):** Se suspende físicamente el contenedor de la pasarela de pagos (`docker stop seats-payment-grpc`), provocando fallos determinísticos a nivel socket TCP.
2. **C2 (Consistencia Eventual Bounded):** Se verifica que el frontend responda de inmediato con `HTTP 202 Accepted` y se evalúa el progreso del estado mediante sondeo activo (*polling*) con límite de tiempo.
3. **C3 (Integridad Transaccional):** Se asegura la transición determinística de la máquina de estados:
   - Happy Path: `Retained (1) -> Confirmed (2)`.
   - Failure Path: `Retained (1) -> Rejected (3)` con motivo `PaymentUnavailable (3)`.
4. **C4 (Liberación de Asientos):** Ante una falla de cobro, el asiento debe ser liberado inmediatamente para evitar retenciones huérfanas en la base de datos transaccional.

---

## 2. Ejecución Automatizada de los Escenarios

### Requisitos Previos
Asegurarse de tener el stack levantado en Docker:
```bash
docker compose up -d
```

### Ejecutar Suite E2E
Desde la raíz del repositorio:
```bash
./scripts/test-e2e-scenarios.sh
```

---

## 3. Matriz de Resultados Esperados

| Escenario | Condición de Entrada | Comportamiento Esperado | Estado Final | Criterio de Éxito |
|:---|:---|:---|:---:|:---:|
| **Happy Path** | Stack 100% operativo | HTTP 202 -> Encolado -> gRPC OK -> Notificación emitida | `Confirmed` (2) | Asiento bloqueado y cobrado |
| **Failure Path** | `seats-payment-grpc` detenido | HTTP 202 -> Booking reintenta vía Polly -> Circuit Breaker | `Rejected` (3) | Asiento liberado; motivo `PaymentUnavailable` |
