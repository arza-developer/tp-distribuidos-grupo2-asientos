# Plan: Availability (solución mínima, Clean Architecture) que reemplaza Availability.Api

## Context
El TP (enunciado en `Desktop\TUP\Programacion 4\Docs tp`, `docs/tasks/TASK_01`) exige que Availability use **Clean Architecture con dominio anémico (3.a)**, se autentique con el Auth Server (3.e), emita logs de auditoría INICIO/FIN con correlationId (3.f), y marque la consistencia eventual (3.g). Responde 202 sin esperar a Booking (paso 3) y consulta el estado a Booking (paso 7).

Los intentos previos (plan `shiny-giraffe`: template Ardalis, FastEndpoints, Specifications, Vogen, Aspire, 3 proyectos de test, net10 + Dockerfile/CI aparte) resultaron en overengineering. Esta vez: **solución mínima que crece por necesidad**.

Estado actual: `src/Services/Availability/Availability/` (sin trackear) tiene scaffold con typos (`Availibility.Web`, `Persistance`), un `Availability.Web` F# huérfano, `SeatNumber` como `int` (el contrato usa `string "A-1"`) y nada de EF/JWT/Azure. `Availability.Api` actual es un stub (asientos mock, `FakeLoggingPublisher`, sin auth real).

## Decisiones asumidas (no se pudieron confirmar con el usuario; ajustables)
- **net8.0** (igual que repo, CI y Dockerfile raíz). Sin global.json ni Directory.Packages.
- **Reconstruir limpio**: se descarta el scaffold actual (incluido el F#); solo se reutiliza la idea del enum `ReservationStatus`.
- **Regla "todo fuera de Availability es solo lectura"**: NO se modifican `Availability.Api`, `Common/SeatsReservation.Contracts`, AuthServer, Booking, Payment, `docker-compose.yml`, `Dockerfile` raíz, `SeatsReservation.slnx` ni CI. Se *leen/referencian* (ProjectReference a Contracts). El cutover se entrega como **documento de pasos propuestos** dentro de la carpeta Availability, no aplicado.
- Vistas MVC: catálogo + reservar + estado.

## Arquitectura (4 proyectos + 1 de tests)
```
Availability/
  Availability.slnx
  src/
    Availability.Domain/          # entidades anémicas (POCO), enums. Sin dependencias.
    Availability.Application/     # Use cases CQRS (MediatR), puertos (interfaces), DTOs, behaviors de logging
    Availability.Infrastructure/  # EF Core + SQLite, AzureQueuePublisher, BookingClient (HttpClient)
    Availability.Web/             # Program.cs, API Controllers, MVC Controller+Views, JWT, Serilog
  tests/Availability.Tests/       # un solo proyecto xunit (unit + WebApplicationFactory)
  Dockerfile                      # propio, dentro de la carpeta
  docs/CUTOVER.md                 # pasos propuestos fuera de la solución
```
Dependencias: Web → Application, Infrastructure; Infrastructure → Application → Domain. Domain sin referencias.

Paquetes: MediatR, Microsoft.EntityFrameworkCore.Sqlite (+Design), Azure.Storage.Queues, Microsoft.AspNetCore.Authentication.JwtBearer 8.0.10, Serilog.AspNetCore, Swashbuckle 6.6.2; tests: xunit, Microsoft.AspNetCore.Mvc.Testing. Sin Ardalis, FastEndpoints, Vogen, Specification, FluentValidation (validación manual en handlers; se agrega solo si hace falta).

Dominio anémico: `Seat { Id, EventId, SeatNumber(string), Price }`, `SeatRequest { ReservationId, EventId, SeatNumber, UserId, Price, Status, RequestedAtUtc, CorrelationId }`, enum `ReservationStatus { Pending, Retained, Confirmed, Rejected, Expired }`. Sin lógica: la orquestación vive en los handlers (justificación 3.a.ii, comentada en código).

CQRS (MediatR): 
- Queries: `GetSeatsQuery(eventId?)`, `GetReservationStatusQuery(id)`
- Commands: `RequestReservationCommand`
Puertos en Application: `ISeatRepository`/`IReservationRequestRepository` (o `IAvailabilityDbContext`), `IMessagePublisher` (reutilizar la firma de `Contracts.IMessagePublisher` y `ReservationRequestedEvent`), `IBookingClient`.

## Contratos HTTP a preservar (de Availability.Api, solo lectura)
- `GET /api/seats` público → `[{seatNumber,price,isAvailable}]` (camelCase); `eventId` opcional.
- `POST /api/reservations` (Bearer) body `{eventId,seatNumber,userId,price}` → **202** `{reservationId,status:"Pending",message,checkStatusUrl:"/api/reservations/{id}"}`; publica `ReservationRequestedEvent` en cola `booking-requests-queue`.
- `GET /api/reservations/{id:guid}` (Bearer) → estado consultado a Booking `GET /api/bookings/{id}`.
- JWT: HS256, issuer `SeatsReservation.AuthServer`, audience `SeatsReservation.Microservices`, clave en configuración (`Jwt:Key`, no hardcodeada en código de Availability; valor dev en appsettings.Development), claim `role`.
- Puerto 5101.

## Fases y tareas (incrementales; cada tarea deja la solución compilando)

### Fase 0 – Estructura base
- T0.0 **Documentación viva** en carpeta propia `src/Services/Availability/Availability/docs/` (la `docs/` raíz del repo no se toca):
  - `docs/PLAN.md`: copia de este plan, con checklist `[ ]/[x]` por tarea y sección "Registro de cambios". Se actualiza al cerrar cada tarea (estado, desvíos respecto del plan, decisiones nuevas).
  - `docs/PROMPTS.md`: **un único archivo** con todos los prompts del usuario, textuales, agrupados por sesión (`## Sesión <fecha> – <rama>`, prompts numerados `P1, P2…` con autor y fecha). Se añade el prompt de cada turno nuevo; el contenido de sesiones previas (prompt inicial de esta sesión y el pedido de guardar plan/prompts) se carga en la creación.
  - `docs/CUTOVER.md` (T6.3) vive en la misma carpeta.
**Verificado en el entorno (2026-11-01):** SDKs 9.0.318 y 10.0.401 (no hay SDK 8); runtimes 8.0.31 / 9 / 10 instalados. Compilar `net8.0` con SDK 10 funciona (el targeting pack se baja por NuGet) y los tests corren sobre runtime 8.0.31. No hay `global.json` ni `Directory.*` en el repo. El `Dockerfile` raíz usa SDK/aspnet 8.0.

**Scaffold actual a reemplazar** (todo sin trackear): `Availability.Domain` (EntityBase, Event, Reservation), `Availability.SharedKernel` (enum), `Availability.UseCase` (IGenericRepository), `Availability.Infrastructure` (vacío, carpeta `Persistance`), `Availibility.Web` (typo, WeatherForecast), `Availability.slnx`, `.vs/`. Nota: el `Availability.Web` F# huérfano ya no está en disco.

**Reglas de la fase:** cada tarea termina con `dotnet build` verde; un commit por tarea (solo si el usuario lo pide); nada fuera de `src/Services/Availability/Availability/` se modifica.

#### T0.1 – Esqueleto de solución y proyectos
1. Eliminar el scaffold actual: carpetas `Availability.*`, `Availibility.Web`, `.vs/` y el `.slnx` viejo. Antes, `git status` para confirmar que nada de esa carpeta está trackeado (el último commit es un revert; ver `docs/` se conserva).
2. Crear layout `src/` + `tests/` (ver Arquitectura) con `dotnet new`:
   - `src/Availability.Domain` (classlib net8.0, sin referencias ni paquetes)
   - `src/Availability.Application` (classlib; ref → Domain)
   - `src/Availability.Infrastructure` (classlib; ref → Application)
   - `src/Availability.Web` (`dotnet new web`, luego se le agrega MVC/Controllers en fases posteriores; ref → Application, Infrastructure)
   - `tests/Availability.Tests` (xunit; ref → Web, Application, Domain)
3. Todos con `<TargetFramework>net8.0</TargetFramework>`, `Nullable` e `ImplicitUsings` habilitados, `LangVersion` por defecto. Sin `Directory.Build.props` por ahora (se agrega solo si se repite configuración ≥3 veces).
4. `dotnet new sln --format slnx -n Availability` y `dotnet sln add` de los 5 proyectos con carpetas de solución `/src/` y `/tests/`.
5. Decisión pendiente para T0.1: **referencia a `SeatsReservation.Contracts`** (`..\..\..\..\..\Common\SeatsReservation.Contracts`). Trae el `payment.proto` y paquetes gRPC al grafo. Recomendado: referenciarlo solo desde `Availability.Application` (para `ReservationRequestedEvent` e `IMessagePublisher`), mantener compatibilidad de contrato y no duplicar tipos. Si el restore o el build resultan pesados/frágiles, alternativa: copiar el contrato mínimo a Application y anotarlo en el registro de cambios. Se decide al ejecutar el paso, no antes.
6. Criterio de hecho: `dotnet build Availability.slnx` verde con 0 warnings; `dotnet sln list` muestra 5 proyectos; grafo de referencias respeta Domain sin dependencias.

#### T0.2 – Web mínimo ejecutable
1. `Program.cs` minimal: `WebApplication.CreateBuilder`, `AddControllers()`, Swashbuckle 6.6.2 (solo `IsDevelopment()`), `MapControllers()`, `MapGet("/health", () => Results.Ok(new { status = "Healthy" }))` anónimo.
2. `partial class Program` al final para habilitar `WebApplicationFactory<Program>` en tests.
3. `Properties/launchSettings.json` con perfil `http` en `http://localhost:5101` (puerto del servicio actual) y `ASPNETCORE_ENVIRONMENT=Development`.
4. `appsettings.json` mínimo (Logging, `AllowedHosts`); las secciones `Jwt`, `AzureQueue`, `Booking`, `ConnectionStrings` se agregan en las fases que las usan (no antes).
5. Sin WeatherForecast, sin vistas, sin EF todavía.
6. Criterio de hecho: `dotnet run --project src/Availability.Web` levanta en 5101 y `GET /health` → 200 `{"status":"Healthy"}`; `/swagger` responde en Development.

#### T0.3 – Proyecto de tests y smoke test
1. `tests/Availability.Tests`: paquetes `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing` (versión 8.0.x, alineada a net8). Sin Moq/FluentAssertions/Shouldly: fakes escritos a mano y `Assert` de xunit.
2. Estructura de carpetas ya preparada: `Unit/` (Application/Domain) y `Api/` (WebApplicationFactory).
3. Test `Api/HealthTests`: `WebApplicationFactory<Program>` → `GET /health` = 200.
4. Criterio de hecho: `dotnet test Availability.slnx` ejecuta 1 test verde. Confirmar que corre con runtime 8.0.31 (no cae a 10 por roll-forward silencioso: revisar la salida de `dotnet test -v n`).

#### T0.4 – Higiene del repo y Dockerfile propio
1. `.gitignore` local (bin/, obj/, .vs/, `*.user`, `*.db`, `*.db-shm`, `*.db-wal`). Revisar primero el `.gitignore` raíz para no duplicar; si ya cubre, no crear uno.
2. `.editorconfig` mínimo: UTF-8, LF/CRLF consistente con el repo, indent 4, file-scoped namespaces como sugerencia.
3. `Dockerfile` dentro de `Availability/` con build context = esta carpeta: stage `sdk:8.0` (restore con csproj copiados primero para cachear capas, `publish` de `src/Availability.Web` a `/out`), stage `aspnet:8.0`, `ASPNETCORE_HTTP_PORTS=5101`, `EXPOSE 5101`, `ENTRYPOINT ["dotnet","Availability.Web.dll"]`. Se añade `.dockerignore` (bin, obj, .vs, docs).
4. Limitación a documentar: la referencia a `SeatsReservation.Contracts` queda fuera del build context de este Dockerfile. Si T0.1 mantiene esa referencia, el `docker build` debe ejecutarse con contexto en la raíz del repo (`-f src/Services/Availability/Availability/Dockerfile .`); se resuelve y documenta aquí, sin tocar el Dockerfile ni compose raíz.
5. Criterio de hecho: `docker build` exitoso y `docker run -p 5101:5101` responde `/health` (si Docker no está disponible localmente, se deja anotado y se verifica solo por `dotnet publish`).

#### Cierre de Fase 0
- Actualizar checklist y "Registro de cambios" de este archivo; agregar el prompt del turno a `docs/PROMPTS.md`.
- Entregable: solución compilando, 1 test verde, servicio sirviendo `/health` en 5101, sin cambios fuera de la carpeta Availability.

### Fase 1 – Dominio y casos de uso de lectura (catálogo)

**Decisiones (usuario, 2026-11-01):** repositorio **por tipo** (`ISeatRepository : IGenericRepository<Seat>`, `SeatRepository : GenericRepository<Seat>`) sobre una base genérica mínima que NO se inyecta directamente (`IGenericRepository<T>` + `GenericRepository<T>` abstracto); `EntityBase` con `int Id` interno y `Guid SeatId` como referencia externa (nunca sale el `int`); `DbInitializer` centraliza migración + seed. Sin entidad `Event`; `Seat.IsAvailable` persistido; DTO público sin `seatId` (contrato idéntico al actual). Fase completa (T1.1–T1.4).

- [x] **T1.1 Dominio anémico** (`src/Availability.Domain`): `Entities/EntityBase` (`int Id`), `Entities/Seat` (`SeatId`, `EventId`, `SeatNumber`, `Price`, `IsAvailable`), `Enums/ReservationStatus` (`Pending, Retained, Confirmed, Rejected, Expired`). Domain sin referencias.
- [x] **T1.2 Application (CQRS)**: MediatR 12.4.1; `Abstractions/IGenericRepository<T>` (`GetByIdAsync`, `FirstOrDefaultAsync`, `ListAsync`, `AddAsync`, `Update`, `Remove`, `SaveChangesAsync`); `Abstractions/ISeatRepository` (`ListByEventAsync`); `Seats/Queries/GetSeats/{GetSeatsQuery,GetSeatsQueryHandler}` (inyecta `ISeatRepository`), `Seats/SeatDto`; `AddApplication()`. Tests unitarios con `InMemorySeatRepository` (fake a mano sobre `InMemoryRepository<T>`).
- [x] **T1.3 Infrastructure**: EF Core Sqlite 8.0.10; `Persistence/{AvailabilityDbContext, GenericRepository (abstracto), SeatRepository, SeedData, DbInitializer, DesignTimeDbContextFactory}`, `Configurations/SeatConfiguration` (PK `Id`, índices únicos `SeatId` y `(EventId, SeatNumber)`), migración `InitialCreate` (solo esquema), `AddInfrastructure(IConfiguration)` (registra `ISeatRepository`; cadena `ConnectionStrings:Availability`, default `Data Source=availability.db`). `DbInitializer.InitializeAsync` = `MigrateAsync` + seed idempotente de los 4 asientos. Tests sobre SQLite temporal (`SqliteTestDb`).
- [x] **T1.4 Web**: `Program.cs` con `AddApplication()`, `AddInfrastructure(config)` y `DbInitializer.InitializeAsync` al arrancar (scope temporal); `SeatsController` `GET /api/seats` `[AllowAnonymous]`, `Guid? eventId` (mal formado → 400); `AvailabilityApiFactory` (SQLite temporal) y `HealthTests` sobre ella; `SeatsContractTests` (200, 4 asientos del mock, camelCase, anónimo, filtro por `eventId`, 400); sección `ConnectionStrings` en `appsettings.json`.

### Fase 2 – Solicitud de reserva (comando) y mensajería

**Objetivo:** `POST /api/reservations` valida que el asiento exista, registra la solicitud localmente (`Pending`), publica `ReservationRequestedEvent` en la cola `booking-requests-queue` (Azure Queue) y responde **202** sin esperar a Booking (paso 3 del TP; frontera de consistencia eventual 3.g). Si el broker falla, no hay 202.

**Contrato a preservar** (de `Availability.Api`, solo lectura): body `{eventId, seatNumber, userId, price}`; 202 con `{reservationId, status:"Pending", message:"Solicitud recibida y encolada para procesamiento", checkStatusUrl:"/api/reservations/{id}"}`; evento = `SeatsReservation.Contracts.Events.ReservationRequestedEvent` y puerto `IMessagePublisher.PublishAsync<T>(msg, queue, ct)` (se reutilizan, no se duplican).

**Decisiones propuestas (ajustables):**
1. **`SeatRequest : EntityBase`** con `int Id` interno y `Guid ReservationId` como referencia externa (índice único). Referencia al asiento por el Guid `SeatId` (no por FK int), más `EventId`, `SeatNumber`, `UserId`, `Price`, `Status` (`ReservationStatus`), `RequestedAtUtc`, `CorrelationId`. Repositorio por tipo: `ISeatRequestRepository : IGenericRepository<SeatRequest>` + `SeatRequestRepository` (interfaz vacía por ahora; `FindByReservationIdAsync` se agrega en Fase 3).
2. **Asiento inexistente → 404** (ProblemDetails). **No se rechaza por `Seat.IsAvailable`**: el flag local puede estar desactualizado y la exclusión mutua la decide Booking (índice único, TASK_02).
3. **Precio:** se publica el precio del catálogo (fuente de verdad); el `price` del body se acepta por compatibilidad pero no se usa (si difiere se loguea warning).
4. **Orden y compensación:** guardar `Pending` → publicar → si falla la publicación, eliminar el registro y devolver **503** (no queda Pending huérfano). Sin outbox (sobreingeniería para el TP); queda anotado como mejora.
5. **Errores de negocio vs. técnicos (T2.0, evaluado contra la arquitectura):** `BusinessException` abstracta en Domain (solo BCL, no agrega comportamiento a las entidades anémicas) con categorías abstractas `BusinessNotFoundException` (→404) y `BusinessValidationException` (→400) y concretas `SeatNotFoundException` / `InvalidReservationRequestException`. Cumple Clean Architecture con una salvedad: los fallos **técnicos** (broker caído) NO son de negocio, por eso `MessagePublishException` vive en Application y no hereda de `BusinessException` (→503). El mapeo a HTTP se hace en Web por categoría con un `IExceptionHandler` (ProblemDetails + `code`), nunca con códigos HTTP en Domain. Sin patrón Result (más ceremonia).
6. **CorrelationId:** puerto `ICorrelationIdAccessor` en Application; en esta fase Web lo implementa con `HttpContext.TraceIdentifier` (como el stub actual); T4.2 lo reemplaza por el header `X-Correlation-ID`.
7. **Auth:** el endpoint queda **sin `[Authorize]` hasta T4.1** (igual que el stub actual); se anota para no olvidarlo.
8. **Logging INICIO/FIN** no se implementa aquí (T4.3, `LoggingBehavior`); el handler solo loguea warning/error de fallos.
9. **Azure Queue:** `Azure.Storage.Queues` 12.x, mensaje JSON (`JsonSerializerDefaults.Web`, camelCase) con `MessageEncoding = Base64` (compatible con consumidores estándar), cola creada si no existe (una vez por nombre), `AzureQueue:ConnectionString` (default `UseDevelopmentStorage=true`, Azurite), reintentos y timeout cortos (`MaxRetries=1`, `NetworkTimeout≈5s`) para fallar rápido con 503. Los errores del SDK se envuelven en `MessagePublishException` (Application no conoce tipos de Azure).

- [x] **T2.0 Excepciones de negocio (paso previo)**: Domain/Exceptions: `BusinessException` (abstracta, `Code`), `BusinessNotFoundException`, `BusinessValidationException`, `SeatNotFoundException` (`seat.not_found`), `InvalidReservationRequestException` (`reservation.invalid_request`); Application/Exceptions: `MessagePublishException` (técnica, no hereda de `BusinessException`). Tests `BusinessExceptionTests` (categorías, códigos, inner exception, Domain sin dependencias).
- [x] **T2.1 Persistencia de solicitudes** (Domain/Application/Infrastructure): entidad `SeatRequest`; `ISeatRequestRepository`/`SeatRequestRepository` + registro DI; configuración EF (índice único `ReservationId`, `Status` como int, `SeatNumber` máx. 20); migración `AddSeatRequests`. Tests: `SeatRequestRepositoryTests` sobre SQLite temporal (alta + búsqueda por `ReservationId` vía `FirstOrDefaultAsync`, unicidad).
- [x] **T2.2 Mensajería Azure Queue** (Application/Infrastructure): `Messaging/QueueNames` (`BookingRequests = "booking-requests-queue"`), `AzureQueueOptions`, `AzureQueuePublisher : IMessagePublisher` (singleton, `QueueClient` cacheado, crea cola), DI + sección `AzureQueue` en `appsettings`. Tests: publicar contra endpoint inalcanzable lanza `MessagePublishException` en pocos segundos (sin Azurite); test de round-trip contra Azurite con atributo `AzuriteFact` que se omite si el puerto 10001 no responde.
- [ ] **T2.3 Caso de uso `RequestReservationCommand`** (Application): `RequestReservationCommand(EventId, SeatNumber, UserId, Price)` → `ReservationAcceptedResult(ReservationId)`; handler (busca asiento por `EventId`+`SeatNumber` vía `ISeatRepository`, valida Guids/seatNumber, guarda `SeatRequest(Pending)`, publica el evento con `ICorrelationIdAccessor`, compensa si falla); lanza `SeatNotFoundException` / `InvalidReservationRequestException` (T2.0) y `ICorrelationIdAccessor`. Comentario XML de arquitectura: paso 3 del TP y frontera de consistencia eventual. Tests unitarios con fakes (`InMemorySeatRequestRepository`, `FakePublisher`): éxito (persistido `Pending`, evento con campos y cola correctos, precio del catálogo), asiento inexistente (nada persistido ni publicado), publicación fallida (registro eliminado, excepción), Guid vacío/seatNumber vacío.
- [ ] **T2.4 Endpoint y tests de contrato** (Web): `ReservationsController` `POST /api/reservations` (DTO con `[Required]`, 202 con el cuerpo del contrato), `ApiExceptionHandler : IExceptionHandler` (mapea por categoría: `BusinessNotFoundException`→404, `BusinessValidationException`→400, `MessagePublishException`→503, resto→500 genérico; ProblemDetails con `code`), `CorrelationIdAccessor` (TraceIdentifier), registros en `Program.cs` (`AddProblemDetails`, `AddExceptionHandler`). `AvailabilityApiFactory` reemplaza `IMessagePublisher` por un fake registrado. Tests: 202 + forma exacta del cuerpo + evento capturado; asiento inexistente → 404; broker caído → 503 y sin `SeatRequest` persistido; body inválido → 400.
- [ ] **T2.5 Verificación manual (condicionada a Docker)**: con Azurite levantado (`docker compose up azurite`, lectura del compose raíz) enviar el POST y comprobar el mensaje en `booking-requests-queue` (puerto 10001). Si Docker no está disponible, queda registrado como pendiente.

**Criterio de cierre de fase:** build 0 warnings, tests verdes (el de Azurite puede quedar omitido), `POST` devuelve 202 con el contrato y publica el evento; sin cambios fuera de la carpeta Availability y sin commits.

### Fase 3 – Seguimiento de estado
- T3.1 `IBookingClient` + `BookingClient` (HttpClient, base URL `Booking:BaseUrl`), mapeo de estados Booking (es: Retenida/Confirmada/Rechazada/Vencida) → enum inglés en un solo punto.
- T3.2 `GetReservationStatusQuery` + handler: consulta a Booking; si falla/404 devuelve estado local `Pending`/"desconocido" según caso.
- T3.3 `GET /api/reservations/{id}` y tests (client fake).
- T3.4 (opcional, solo si se necesita) `isAvailable` del catálogo consciente de reservas Retenidas/Confirmadas conocidas localmente.

### Fase 4 – Seguridad y observabilidad
- T4.1 JWT Bearer en Web (`AddJwtAuthentication` local, configuración por `Jwt:*`), `[Authorize]` en POST/GET de reservas; `GET /api/seats` queda `[AllowAnonymous]`. Tests 401/202.
- T4.2 Serilog (consola) con enricher de correlationId; middleware `X-Correlation-ID` (leer/generar y devolver header).
- T4.3 `LoggingBehavior<,>` de MediatR: `[{CorrelationId}] INICIO: ...` / `FIN: ...` por request (3.f).
- T4.4 `BookingClient` adjunta token M2M: pedir token al Auth Server (`POST /connect/token`, lectura del contrato) con caché hasta expiración.

### Fase 5 – MVC mínimo
- T5.1 `HomeController` (ControllerWithViews) con vista de catálogo (`GetSeatsQuery` vía MediatR), layout Razor mínimo sin framework JS.
- T5.2 Acción "Reservar" (POST con antiforgery) → `RequestReservationCommand`; redirige a página de estado.
- T5.3 Vista de estado con refresco manual (`GetReservationStatusQuery`).

### Fase 6 – Cierre y entrega
- T6.1 Comentarios arquitectónicos (patrones/decisiones, ítem 3 del TP) y diagrama de capas Availability (TASK_10), marcando frontera de consistencia eventual (3.g: tras el 202 / enlaces Availability→Booking).
- T6.2 Flujo feliz y flujo de fallo (broker caído, Booking caído) con tests (3.j).
- T6.3 `docs/CUTOVER.md`: cambios propuestos (NO aplicados) en `Dockerfile` raíz/compose (`SERVICE_NAME`/ruta del proyecto, `Jwt__Key`, `AzureQueue__ConnectionString`, `Booking__BaseUrl`, `ConnectionStrings__Default`/volumen SQLite), `SeatsReservation.slnx` y CI, más retiro de `Availability.Api`. Se aplican solo cuando el usuario lo autorice.

## Verificación
- `dotnet build` y `dotnet test` en `Availability.slnx` tras cada tarea.
- Ejecutar Web en 5101 y probar con `tests/api-tests.http` (lectura): token en AuthServer (`/connect/token`), `GET /api/seats`, `POST /api/reservations` con Bearer → 202, sin token → 401, `GET /api/reservations/{id}`.
- Azurite (compose existente, lectura): verificar mensaje en `booking-requests-queue` (puerto 10001).
- Revisar logs `[{CorrelationId}] INICIO/FIN`.
- `git status` al final: cambios solo bajo `src/Services/Availability/Availability/`.

## Archivos clave de referencia (solo lectura)
- `src/Services/Availability.Api/Controllers/ReservationsController.cs` (contratos)
- `src/Common/SeatsReservation.Contracts/Events/ReservationEvents.cs` (`ReservationRequestedEvent`, `IMessagePublisher`)
- `src/Services/AuthServer.Api/Program.cs` (issuer/audience/clave/claims)
- `docs/tasks/TASK_01_AVAILABILITY_CLEAN_ARCH.md`, `tests/api-tests.http`, `docker-compose.yml`

---

## Estado de tareas (actualizar al cerrar cada una)
- [x] T0.0 Documentación viva (`docs/PLAN.md`, `docs/PROMPTS.md`)
- [x] T0.1 Esqueleto de solución
- [x] T0.2 Web mínimo ejecutable
- [x] T0.3 Proyecto de tests y smoke test
- [x] T0.4 Higiene del repo y Dockerfile (Fase 0 completa; imagen Docker sin verificar, ver registro)
- [x] T1.1 – T1.3 Fase 1 (dominio, query CQRS, EF+SQLite+DbInitializer)
- [x] T1.4 Endpoint GET /api/seats y tests de contrato (Fase 1 completa)
- [x] T2.0 Excepciones de negocio (paso previo)
- [x] T2.1 Persistencia de solicitudes
- [x] T2.2 Mensajería Azure Queue
- [ ] T2.3 – T2.5 Fase 2
- [ ] T3.1 – T3.4 Fase 3
- [ ] T4.1 – T4.4 Fase 4
- [ ] T5.1 – T5.3 Fase 5
- [ ] T6.1 – T6.3 Fase 6

## Registro de cambios
- 2026-11-01: plan aprobado. Creada carpeta `docs/` con `PLAN.md` y `PROMPTS.md` (T0.0). Nota: esta copia vive en el repo; el plan original está en `~/.claude/plans/` y puede quedar desactualizado, la fuente de verdad es este archivo.
- 2026-11-01: Fase 0 detallada (T0.1–T0.4 con pasos y criterios de hecho). Verificado entorno: SDK 9/10, runtime 8.0.31; scaffold actual solo contiene proyectos viejos (sin F#).
- 2026-11-01: T0.1 completada. Scaffold viejo eliminado (la carpeta `.vs/` quedó porque Visual Studio la tiene bloqueada; se ignora en T0.4). Creados `src/` (Domain, Application, Infrastructure, Web) y `tests/Availability.Tests` en net8.0 con `Availability.slnx`. Decisión: `Availability.Application` referencia `SeatsReservation.Contracts` (ProjectReference, solo lectura); build con 0 warnings/0 errores, así que no se copia el contrato. Sin commits (los hace el usuario).
- 2026-11-01: T0.2 completada. `Program.cs` con `AddControllers`, Swagger (solo Development), `GET /health` anónimo y `partial class Program`. `launchSettings.json` reducido al perfil `http` en 5101 (se quitaron IIS Express/https del template). Swashbuckle 6.6.2. Verificado en ejecución: `/health` → 200 `{"status":"Healthy"}`, `/swagger` → 200. `appsettings` sin cambios (secciones se agregan por fase). Sin commits.
- 2026-11-01: T0.3 completada. `Availability.Tests` con `Microsoft.AspNetCore.Mvc.Testing` 8.0.10 (sin librerías de mocks); carpetas `Unit/` (con `.gitkeep`) y `Api/`; `Api/HealthTests` (`WebApplicationFactory<Program>` → `/health` 200). `dotnet test Availability.slnx`: 1 superado, 0 fallos, ejecutado como net8.0. Sin commits.
- 2026-11-01: T0.4 completada (Fase 0 cerrada). `.gitignore` local solo con `*.db/-shm/-wal` (el raíz ya cubre bin/obj/.vs/*.user), `.editorconfig` mínimo, `Dockerfile` propio (restore por capas, publish a /out, aspnet:8.0, puerto 5101) y `Dockerfile.dockerignore` junto a él. El build debe ejecutarse con contexto en la raíz del repo: `docker build -f src/Services/Availability/Availability/Dockerfile -t availability .` (por la referencia a `SeatsReservation.Contracts`). **Pendiente de verificar:** `docker build`/`run` no se pudo probar porque Docker Desktop no estaba corriendo; se verificó `dotnet publish -c Release` (genera `Availability.Web.dll` y `SeatsReservation.Contracts.dll`). Sin commits.
- 2026-11-01: Fase 1 detallada y ejecutadas T1.1–T1.3 (T1.4 pendiente por indicación del usuario). Nota técnica: no hay `Program.cs` conectado a EF todavía, por eso se agregó `DesignTimeDbContextFactory` (solo para `dotnet ef`); la migración se generó con `dotnet-ef` global 10.0.14 sobre EF Core 8.0.10 sin problemas. Verificación: `dotnet build` 0 warnings, `dotnet test` 11 superados (handler, `DbInitializer` idempotente, repositorio genérico). Sin commits.
- 2026-11-01: Refactor de T1.2/T1.3 por indicación del usuario: `IGenericRepository<T>` es solo contrato base mínimo y no se usa directamente. Se agregó `ISeatRepository : IGenericRepository<Seat>` (con `ListByEventAsync`) y `SeatRepository : GenericRepository<Seat>`; `GenericRepository<T>` pasó a abstracto (db `protected`) y el DI registra solo `ISeatRepository`. Regla: cada entidad nueva = `IXRepository` + `XRepository` + registro. Tests: `InMemorySeatRepository`, `SeatRepositoryTests` (+ `ListByEventAsync`). Build 0 warnings; 12 tests superados. Sin commits.
- 2026-11-01: T1.4 completada (Fase 1 cerrada). `Program.cs` registra `AddApplication()`/`AddInfrastructure()` y ejecuta `DbInitializer.InitializeAsync` al arrancar; `SeatsController` `GET /api/seats` anónimo con `eventId` opcional (mal formado → 400). Desvío respecto del detalle: `AddInfrastructure()` ya no recibe `IConfiguration`; resuelve `ConnectionStrings:Availability` de forma diferida desde el contenedor para que los overrides de test (`AvailabilityApiFactory`, SQLite temporal) apliquen de forma fiable. `appsettings.json` con `ConnectionStrings`. Tests: `SeatsContractTests` (4 casos) + `HealthTests` sobre la factory. Verificación: build 0 warnings; 16 tests superados; en ejecución real `GET /api/seats` devolvió exactamente el JSON del mock (camelCase, A-3 ocupado), `eventId` conocido → 4, desconocido → `[]`, `eventId=abc` → 400. Sin commits.
- 2026-11-01: Fase 2 detallada (T2.1–T2.5, ahora ordenadas de abajo hacia arriba: persistencia → mensajería → caso de uso → endpoint → verificación con Azurite). Se registran 9 decisiones propuestas (SeatRequest con Guid externo, 404 por asiento inexistente sin validar `IsAvailable`, precio del catálogo, compensación sin outbox, excepciones + `IExceptionHandler`, `ICorrelationIdAccessor`, sin `[Authorize]` hasta T4.1, Azure Queue con Base64/JSON camelCase y timeouts cortos). Sin ejecutar.
- 2026-11-01: T2.0 completada (paso previo de la Fase 2). Evaluación: `BusinessException` abstracta en Domain **cumple** la arquitectura (solo BCL, sin comportamiento en entidades, handlers lanzan y Web traduce) con una salvedad resuelta con alternativa: los fallos técnicos (`MessagePublishException`, en Application) no son de negocio y no heredan de ella; el HTTP se mapea por categoría en Web. Creadas 5 clases en Domain/Exceptions + `MessagePublishException`; 5 tests nuevos. Build 0 warnings; 21 tests superados. T2.1–T2.5 pendientes. Sin commits.
- 2026-11-01: T2.1 completada. `SeatRequest : EntityBase` (Id interno, `ReservationId` Guid externo único, `SeatId` Guid del asiento, `Status` como int); `ISeatRequestRepository`/`SeatRequestRepository` (vacíos sobre la base genérica, registrados en DI); `SeatRequestConfiguration`; migración `AddSeatRequests` (solo tabla + índice único). 4 tests nuevos (`SeatRequestRepositoryTests`: alta, búsqueda por `ReservationId`, duplicado → `DbUpdateException`, borrado). Verificado en arranque real: la BD queda con `Seats`, `SeatRequests` y `__EFMigrationsHistory`, y `/api/seats` sigue en 200. Build 0 warnings; 25 tests superados. Sin commits.
- 2026-11-01: T2.2 completada. Application: `Messaging/QueueNames` (`booking-requests-queue`). Infrastructure/Messaging: `AzureQueueOptions` (sección `AzureQueue`, default `UseDevelopmentStorage=true`, `MaxRetries=1`, `NetworkTimeoutSeconds=5`), `QueueMessageSerializer` (JSON camelCase), `AzureQueuePublisher : IMessagePublisher` (singleton, `QueueClient` cacheado por cola, crea la cola una vez, Base64, errores → `MessagePublishException`). Paquetes: `Azure.Storage.Queues` 12.21.0 y `Microsoft.Extensions.Options.ConfigurationExtensions` 8.0.0 (necesario para `BindConfiguration`). DI registra `IMessagePublisher` y las opciones; `appsettings.json` con sección `AzureQueue`. Tests (`Messaging/`): contrato del nombre de cola, forma del JSON, broker inalcanzable → `MessagePublishException` en segundos, connection string inválida, y round-trip contra Azurite con `AzuriteFact` (se omite si el puerto 10001 no responde). Verificación: build 0 warnings; 29 tests + 1 omitido sin Azurite; con un Azurite temporal (contenedor `av-azurite-test`, ya eliminado) 30/30 superados. Nota: en contenedores la cadena debe apuntar al host de Azurite (va a `docs/CUTOVER.md`). Sin commits.
