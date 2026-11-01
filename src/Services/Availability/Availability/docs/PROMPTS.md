# Registro de prompts – Availability

Archivo único. Un bloque por sesión; los prompts del usuario se transcriben textualmente (incluidos errores de tipeo). Se agrega el prompt de cada turno nuevo al final de su sesión.

---

## Sesión 2026-11-01 – rama `develop` – Autor: FHP

### P1
```
Se necesita desarrollar implementacion de solucion Availability, que reemplace solucion ya existente Availability.Api. Esta implementacion ya se habia desarrollado con planes anteriores pero resulto en un overengeneering excesivo, por eso se busco precindir de template Ardalis y desarrollar una solucion minima a incrementar segun necesidades. Recuperar analisis de documentacion sobre TP. Para la arquitectura se busca Clean Architechture con Entidades animicas, Use Cases con patron CQRS utilizando libreria MediatR. Para la infraestructura se utilizara EF con motor SQLite para ORM, message broker Azure Qeue. Se utilizara tanto API Controller como ControllerWhitView minimos. Se debe implementar un logger y JWT para auntenticar mediante auth server. El Plan de  desarrollo debe ser incremental separado en tareas y agrupadas por fases tomando desde la estructuracion basica del proyecto hasta cubrir funcionalidad total. Toda accion por fuera de la presente solucion Availability debe ser de solo lectura
```

### P2
```
guardar plan
```
(Respuesta del usuario al rechazar la salida del modo plan.)

### P3
```
Guardar registro de prompts por sesion y planes en archivos dentro de nueva carpeta docs propia. El archivo de plan se debe actualizar segun implementaciones, y los textos de prompts se deben unificar en un mismo archivo
```

### P4
```
Profundizar en las tareas para Fase 0
```

### P5
```
Sí, pero nunca realizar commit automatico de cambios. Siempre deben realizarse de forma manual por usuario
```

### P6
```
Sí, seguí con T0.2
```

### P7
```
Sí, seguí con T0.3
```

### P8
```
Sí, seguí con T0.4
```

### P9
```
Detallar pasos para Fase 1
```

### P10
```
Mantener abstraccion generica de repositorio para evitar codigo duplicado. En entidades mantener EntityBase con Id int que permita identificacion interna de Id en BBDD local y utilizar campo Guid SeatId que sera la referencia externa para evitar posibles inconsistencia local en datos por caida de Broker o Booking
```

### P11
```
Sumar un metodo DbInitializer o similar para centralizar tareas de seeder
```

### P12
```
Realizar hasta tarea 1.3
```

### P13
```
IGenericRepository no debe utilizarse directamente, es una interfaz minima, que debe implementarse por tipo ejemeplo IRepositorySeat que permita extender comportamiento si es necesario
```

### P14
```
Sí, seguí con T1.4
```

### P15
```
Sí, detallá Fase 2
```

### P16
```
Antes de implementar sumar un paso previo de definicion de BussnesExcepciones con definicion abstracta en Domain, evaluar si cumple con arquitectura actual y si no ofrecer alternativa
```

### P17
```
Sí, seguí con T2.1
```

### P18
```
Seguir con T2.2
```

### P19
```
Sí, seguí con T2.3
```

### P20
```
Seguir con T2.4
```

### P21
```
realizar T2.5
```

### P22
```
Se volvieron asincronos metodos de update y remove en repositorios, corregir implementacion en test unit
```

### P23
```
Alinear también los tests al nuevo comportamiento
```
(Respuesta del usuario a la consulta de alcance: "Alinear también los tests al nuevo comportamiento".)

### P24
```
Antes confirmar si metodos update y delete de EF ejecutan la accion contra bbdd o solo afectan trackin de entidad y espera a savechanges. De ser asi volver a hacer estos metodos sincronos
```

### P25
```
corregir punto 1 y punto 2. Anotar como tech debt definir flujo para volver Seat.IsAvailable, este flag es para robustecer guard de idempotencia y concurrencia ante posibles estados de consistencia eventual con booking
```

### P26
```
Sí, detallá Fase 3
```

### P27
```
Realizar puntos 3.1 y 3.2
```

### P28
```
Aprobar plan e implementar hasta punto 3.2
```

### P29
```
Sí, seguí con T3.3
```

### P30
```
Sí, seguí con T3.4
```

### P31
```
Sí, hacé T3.5
```

### P32
```
Antes de seguir con fase 4. Profundizar con resolucion de tech debt TD-01 para limitar situaciones de seat unavailable. Tambien eliminar test UpdateRemoveTrackingTests, ya se comprobo funcionalidad de librerias ya no se necesitan
```

### P33
```
Por ahora sincronizar al consultar, actualizar TD-01 expresando posibles alternativas con worker de reconciliacion pero metodo get nuevo en booking que devuelva List para evitar escalado de trabajo por acumulacion
```
(Respuesta del usuario a la consulta sobre cómo resolver TD-1.)
