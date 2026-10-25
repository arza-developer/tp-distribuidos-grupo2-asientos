# 📋 Especificación Técnica: Tarea 08
**Rama Git:** `feature/08-e2e-testing-diagrams`  
**Módulo:** `tests/` y `docs/`  
**Puerto:** N/A (Automatización y Documentación)  

## 1. Alcance y Responsabilidad
Construir los escenarios de prueba ejecutables de extremo a extremo y generar los diagramas arquitectónicos de entrega.

## 2. Requisitos de Cátedra
- **Requisito 3.j:** Flujo Happy Path y Flujo de Fallo ejecutable.
- **Requisitos 3.g, 3.h, 3.i:** Diagramas de componentes, consistencia eventual y tabla de componentes.
- **Requisito 2:** Historial de prompts consolidado (`prompts_memory.md`).

## 3. Contratos y Herramientas
- Archivo ejecutable: `tests/api-tests.http` con soporte para `httpyac`.

## 4. Tareas Concretas
1. Completar `tests/api-tests.http` con dos suites: Happy Path y Fallo de componente.
2. Generar diagramas de arquitectura para cada API (Clean para Availability, N-Layer para Booking y Payment).
3. Recopilar los prompts de los integrantes en `docs/prompts_memory.md`.

## 5. Criterios de Aceptación (DoD)
- [ ] `npx httpyac tests/api-tests.http --all` ejecuta el flujo completo con 100% de éxito.
- [ ] Demostración clara del fallo ante el profesor apagando un contenedor.

## 6. Defensa Oral
- **¿Cómo probaron la resiliencia del sistema?**  
  *Respuesta:* Demostrando el escenario de fallo donde un componente apagado no tira el sistema abajo, sino que degrada elegantemente.
