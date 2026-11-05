# Registro de Decisiones de Arquitectura (ADR)
**Producto:** Sistema de Reserva de Asientos - Grupo 2  
**Cátedra:** Programación IV / Sistemas Distribuidos - UTN FRBA  

| ADR | Título | Requisito / Tema | Estado |
|:---|:---|:---|:---:|
| [ADR-001](ADR-001-estilo-microservicios.md) | Estilo de Microservicios y Aislamiento de Persistencia | Requisito 3.d (Topología y DB) | Aprobado |
| [ADR-002](ADR-002-patrones-heterogeneos-dominio-anemico.md) | Patrones Heterogéneos y Dominio Anémico | Requisitos 3.a, 3.b, 3.c (Clean vs N-Layer) | Aprobado |
| [ADR-003](ADR-003-consistencia-eventual-broker.md) | Consistencia Eventual y Desacoplamiento con Broker | Requisito 3.g, Paso 3 (Brewer AP) | Aprobado |
| [ADR-004](ADR-004-control-concurrencia-asientos.md) | Control de Concurrencia y Exclusión Mutua sobre Asientos | Paso 4 (Carrera sobre el asiento) | Aprobado |
| [ADR-005](ADR-005-expiracion-reservas-ttl.md) | Ciclo de Vida de Reservas y Mecanismo de Expiración TTL | Paso 8 (Vencimiento de retención) | Aprobado |
| [ADR-006](ADR-006-comunicacion-grpc-polly-pago.md) | Comunicación gRPC y Resiliencia con Polly | Requisitos 3.c, 3.j (Fallas y Circuit Breaker) | Aprobado |
| [ADR-007](ADR-007-seguridad-m2m-jwt.md) | Seguridad, Autenticación M2M y Validación de Tokens | Requisito 3.e, Paso 2 (OAuth2 / OIDC) | Aprobado |
| [ADR-008](ADR-008-servicio-notificaciones-autonomo.md) | Desacoplamiento del Servicio de Notificaciones | Requisito 3.h (Consumidor de eventos) | Aprobado |
| [ADR-009](ADR-009-trazabilidad-auditoria-correlation-id.md) | Trazabilidad Distribuida y Auditoría mediante CorrelationId | Requisito 3.f (Cadena de sucesos) | Aprobado |
