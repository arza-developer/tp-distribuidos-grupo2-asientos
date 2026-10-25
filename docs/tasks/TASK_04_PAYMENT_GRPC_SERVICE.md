# 📋 Especificación Técnica: Tarea 04
**Rama Git:** `feature/04-payment-grpc-service`  
**Módulo:** `src/Services/Payment.Grpc/`  
**Puerto:** `HTTP/2 5103`  

## 1. Alcance y Responsabilidad
Implementar el microservicio de procesamiento financiero por gRPC con persistencia contable e inyección de fallos controlados.

## 2. Requisitos de Cátedra
- **Tecnología:** gRPC binario sobre HTTP/2 y Protobuf (Requisito 3.c).
- **Patrón:** N-Layer + Dominio Anémico.
- **Resiliencia:** Capacidad de simular caídas para probar el Circuit Breaker (Punto 3.j).

## 3. Contratos y Endpoints
- Archivo Proto: `SeatsReservation.Contracts/Protos/payment.proto` (método `ProcessPayment`).

## 4. Tareas Concretas
1. Implementar la clase base `PaymentApi.PaymentApiBase` autogenerada.
2. Persistir transacciones en SQLite (`payment.db`) con TransactionId inmutable.
3. Agregar switch de configuración `SimulateFailure: true/false` para forzar errores en pruebas de caos.

## 5. Criterios de Aceptación (DoD)
- [ ] Responde llamadas gRPC con latencia menor a 20ms en flujo normal.
- [ ] Al activar `SimulateFailure`, devuelve error o rechazo financiero simulado.

## 6. Defensa Oral
- **¿Por qué gRPC en lugar de REST para pagos?**  
  *Respuesta:* Por el contrato binario estricto (Protobuf), menor sobrecarga de serialización y multiplexación HTTP/2 requerida en microservicios internos.
