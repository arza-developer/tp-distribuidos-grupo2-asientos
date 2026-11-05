# Historial de Prompts - Lucas Ojeda
**Rol:** Tarea 08 - Testing E2E y Verificación de Caos  
**Bitácora Detallada:** [Ver Bitácora de Pruebas en Tarea 08](../tasks/TASK_08_E2E_TESTING_CHAOS.md)

## Resumen de Prompts Principales
- Configuración de la suite ejecutable en `tests/api-tests.http` con extensión httpYac.
- Diagnóstico de puertos ocupados en Windows (5101/5103) con `netstat` y resolución de arranque Docker.
- Implementación del flujo de Happy Path (login, catálogo, reserva 202 y sondeo) y Chaos Path ante la caída de pagos.
