# ADR-002: Patrones Heterogéneos (Clean Architecture vs. N-Layer) y Dominio Anémico

**Estado:** Aprobado  
**Fecha:** 2026-10-25  

## 1. Contexto y Restricciones
- **Requisitos 3.a, 3.b, 3.c:** `Availability` debe implementar Clean Architecture; `Booking` y `Payment` deben implementar N-Layer; los tres deben usar Dominio Anémico.
- **Naturaleza de los Servicios:** `Availability` concentra integraciones periféricas complejas (UI Razor, REST, Auth OIDC, Broker, SQLite). `Booking` y `Payment` procesan tuberías transaccionales lineales.

## 2. Alternativas Evaluadas

### Opción A: Clean Architecture Homogénea en Todos los Servicios
- **Ventajas:** Estructura uniforme en todo el repositorio.
- **Desventajas:** Sobrecarga innecesaria de abstracciones (múltiples proyectos, puertos, DTOs y mapeos) en `Booking` y `Payment`, donde la lógica se reduce a validar un índice y llamar a una pasarela. Viola la consigna 3.b y 3.c.

### Opción B: N-Layer Homogéneo en Todos los Servicios
- **Ventajas:** Simplicidad y rapidez de implementación.
- **Desventajas:** En `Availability`, el acoplamiento descendente directo entre controladores y acceso a datos dificulta cambiar componentes de infraestructura sin alterar la lógica de aplicación. Viola la consigna 3.a.

### Opción C: Asignación Heterogénea y Dominio Anémico (Seleccionada)
- **Ventajas:** Cumple taxativamente la consigna. Aplica inversión de dependencias estricta en el punto de entrada (`Availability`) y mantiene simplicidad procedimental en los motores internos (`Booking`, `Payment`).
- **Desventajas:** Ruptura de consistencia estructural entre proyectos del mismo repositorio.

## 3. Decisión
1. `Availability` se estructura en 4 proyectos con dependencias hacia el centro: `Domain` (POCO puro), `Application` (casos de uso y puertos), `Infrastructure` (adaptadores SQLite/Broker) y `Web` (controladores y vistas).
2. `Booking` y `Payment` se organizan en 3 capas clásicas dentro de un único proyecto (`Presentation`, `Business`, `DataAccess`).
3. Las entidades (`Seat`, `Reservation`) son anémicas: sólo exponen propiedades de datos. La lógica transaccional y validaciones residen en los servicios de negocio (`BookingService`, handlers).

## 4. Costo de Mantenimiento y Mitigaciones
- **Falta de Encapsulamiento en Entidades:** Las entidades no autovalidan sus invariantes.  
  *Mitigación:* Se centralizan las reglas de transición de la máquina de estados en `BookingService` y se cubren con pruebas de integración.
