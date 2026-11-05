# ADR-001: Estilo de Microservicios y Aislamiento de Persistencia

**Estado:** Aprobado  
**Fecha:** 2026-10-25  

## 1. Contexto y Restricciones
- **Requisito 3.d:** Adopción obligatoria del estilo de microservicios.
- **Concurrencia sobre el Asiento:** Múltiples clientes compiten por reservar el mismo recurso físico simultáneamente.
- **Réplicas de Procesamiento:** La carga transaccional se distribuye entre réplicas de `Booking` (Paso 4).
- **Catálogo Abierto:** `Availability` debe permitir navegación pública sin autenticación (Paso 1).
- **Persistencia:** SQLite local montado en volúmenes Docker independientes.

## 2. Alternativas Evaluadas

### Opción A: Monolito Modular
- **Ventajas:** Transacciones ACID inmediatas en memoria/base local, cero latencia de red interna.
- **Desventajas:** Viola el Requisito 3.d. La contención en el cobro bloquea los hilos del servidor web, degradando la navegación del catálogo público. Escalamiento exclusivamente vertical.

### Opción B: Microservicios con Base de Datos SQLite Compartida
- **Ventajas:** Mantiene procesos independientes pero permite consultas directas relacionales entre catálogo y reservas.
- **Desventajas:** SQLite bloquea el archivo entero ante escrituras (*file-level lock*). Las réplicas de `Booking` bloquean el archivo al asentar reservas, provocando excepciones de tipo `database is locked` en las lecturas de `Availability`.

### Opción C: Microservicios con Database-per-Service (Seleccionada)
- **Ventajas:** Desacopla el I/O de disco. `Availability` atiende lecturas del catálogo en `availability.db` sin contención por las escrituras transaccionales de `Booking` en `booking.db`.
- **Desventajas:** Pérdida de atomicidad transaccional directa entre catálogo y reserva; exige consistencia eventual.

## 3. Decisión
Se implementa el estilo de microservicios con aislamiento estricto de persistencia (**Database-per-Service**). La transferencia de solicitudes de reserva hacia las réplicas de `Booking` se realiza de manera asíncrona mediante un Message Broker.

## 4. Costo de Mantenimiento y Mitigaciones
- **Costo de Operación:** Requiere orquestar múltiples contenedores y redes virtuales en Docker.
- **Inconsistencia Temporal:** El catálogo no refleja la confirmación de inmediato.  
  *Mitigación:* Se responde `HTTP 202 Accepted` y se implementa sondeo de estado (*polling*) en el cliente.
