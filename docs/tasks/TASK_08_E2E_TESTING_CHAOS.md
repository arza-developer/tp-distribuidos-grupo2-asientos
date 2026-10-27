# 📋 Especificación Técnica: Tarea 08
**Rama Git:** `feature/08-e2e-testing-diagrams`  
**Módulo:** `tests/`  
**Tipo:** Automatización de Pruebas y Caos  

## 1. Alcance y Responsabilidad
Construir y mantener la suite ejecutable de pruebas de extremo a extremo que demuestre ante el profesor el camino feliz y la recuperación ante caídas de componentes.

## 2. Requisitos de Cátedra
- **Requisito 3.j:** La solución final debe compilar y ejecutar un flujo de Happy path y uno de fallo si un componente falta.

## 3. Contratos y Herramientas
- Archivo ejecutable: `tests/api-tests.http` con soporte para `httpyac`.

## 4. Tareas Concretas
1. Completar `tests/api-tests.http` con dos suites: Happy Path y Fallo de componente.
2. Desarrollar script de prueba de caos: apagar Payment (`docker stop seats-payment-grpc`) y verificar que Booking rechaza la reserva por pasarela no disponible sin colgar el sistema.
3. Asegurar ejecución de pruebas desde terminal mediante `npx httpyac tests/api-tests.http --all`.

## 5. Criterios de Aceptación (DoD)
- [ ] `npx httpyac tests/api-tests.http --all` ejecuta el flujo completo con 100% de éxito.
- [ ] Demostración clara del escenario de fallo ante el profesor.

## 6. Defensa Oral
- **¿Cómo probaron la resiliencia del sistema?**  
  *Respuesta:* Demostrando el escenario de fallo donde un componente apagado no tira el sistema abajo, sino que degrada elegantemente vía Circuit Breaker.
