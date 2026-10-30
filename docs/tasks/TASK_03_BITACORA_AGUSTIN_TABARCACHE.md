# Bitácora: Tarea 03 (Booking Worker + Expiración)

**Integrante:** Agustin Tabarcache
**Rama:** `feature/03-booking-worker-expiration`
**Especificación:** [TASK_03_BOOKING_WORKER_EXPIRATION.md](TASK_03_BOOKING_WORKER_EXPIRATION.md)

Registro de lo que se va haciendo en la tarea, tanto lo hecho por el responsable como lo hecho con asistencia de IA.

## Estado actual

| # | Trabajo | Estado |
|---|---------|--------|
| 1 | `BookingQueueConsumer`: escuchar la cola de Azure Service Bus y registrar reservas | Hecho |
| 2 | `ReservationExpirationWorker`: vencer reservas retenidas cada 15-30 s | Hecho |
| 3 | Liberación del asiento al vencer | Resuelto por diseño y verificado (prueba 6c) |

## Plan del trabajo 1: `BookingQueueConsumer`

- [x] ~~Paso 1: crear la cola en Azure Service Bus.~~ No hace falta: el docente ya entregó el topic `grupo2-booking-topic`.
- [x] Paso 1b: conseguir el nombre de la suscripción del topic: `booking` (según el ejemplo del docente).
- [x] Paso 2: instalar el paquete `Azure.Messaging.ServiceBus` versión `7.21.0` (la misma del ejemplo del docente).
- [x] Paso 3: guardar el connection string con `dotnet user-secrets` (clave `ServiceBus:ConnectionString`).
- [x] Paso 4: agregar `ServiceBus:TopicName`, `ServiceBus:SubscriptionName` y `ConnectionStrings:BookingDataBase` en `appsettings.json`.
- [x] Paso 5: crear `Workers/BookingQueueConsumer.cs`.
  - [x] 5a: esqueleto (`BackgroundService` + `ServiceBusProcessor` que escucha el topic y registra cada mensaje en el log).
  - [x] 5b: procesar el mensaje (deserializar `ReservationRequestedEvent` y registrar la reserva en la base).
- [x] Paso 6: registrar `ServiceBusClient` y el hosted service en `Program.cs`.
- [x] Paso 7: `dotnet ef database update` y `dotnet run`.
- [x] Paso 8: probar enviando un mensaje JSON a mano, con un `ServiceBusSender` como el del ejemplo del docente o desde Service Bus Explorer.

## Plan del trabajo 2: `ReservationExpirationWorker`

- [x] Paso 1: agregar al repositorio el método `ExpireOverdueReservationsAsync(DateTime nowUtc, CancellationToken)`.
  - Usa `ExecuteUpdateAsync` de EF Core: un solo `UPDATE ... SET Status = 4 WHERE Status = 1 AND ExpiresAtUtc <= now` directo en la base, sin cargar las reservas en memoria.
  - Devuelve la cantidad de reservas vencidas, para registrarla en el log.
- [x] Paso 2: agregar `ReservationExpiration:IntervalSeconds` (`15`) en `appsettings.json`.
- [x] Paso 3: crear `Workers/ReservationExpirationWorker.cs`.
  - `BackgroundService` con un `PeriodicTimer` que se dispara cada `IntervalSeconds`.
  - Un scope por ciclo con `IServiceScopeFactory`, igual que en el consumer.
  - `try/catch` por ciclo: si un ciclo falla (por ejemplo, SQLite bloqueada), se registra el error y el worker sigue con el próximo.
- [x] Paso 4: registrar el worker en `Program.cs` con `AddHostedService<ReservationExpirationWorker>()`.
- [x] Paso 5: `dotnet build` y `dotnet run`.
- [x] Paso 6: probar.
  - [x] 6a: al arrancar, la reserva `1111...` (`Retained` desde la prueba del trabajo 1, ya vencida) pasa a `Expired`.
  - [x] 6b: enviar una reserva nueva (`3333...`, asiento `A2`), esperar 5 minutos y ver que pasa a `Expired`.
  - [x] 6c: enviar otra reserva para `A2` (`4444...`): queda `Retained`. Confirma que el asiento se liberó (trabajo 3).
- [x] Paso 7: commit.

## Decisiones

- **Broker:** se usa Azure Service Bus, no Azurite. Azurite solo emula Blob, Queue y Table, no Service Bus.
- **~~Cola `booking-requests-queue`~~ → Topic `grupo2-booking-topic`:** el docente entregó un connection string con `EntityPath=grupo2-booking-topic`. Es un topic (publicación y suscripción), no una cola. Booking lee a través de una suscripción propia: `CreateProcessor(topic, suscripción)`. Configuración: `ServiceBus:TopicName` y `ServiceBus:SubscriptionName`.
- **Secreto:** el connection string incluye la clave de acceso. Se guarda solo en `dotnet user-secrets`, nunca en `appsettings.json` ni en el repositorio.
- **Liberación del asiento:** el índice único filtrado `(EventId, SeatNumber)` con `Status IN (1, 2)` solo considera reservas `Retained` o `Confirmed`. Al pasar una reserva a `Expired` (4), el asiento queda libre sin código adicional.
- **Idempotencia:** el `ReservationId` del evento se guarda como `RequestId`. Si llega un mensaje repetido, se descarta.
- **Retención:** 5 minutos desde que se registra la reserva.
- **Mapeo:** `UserId` llega como `Guid` en el evento y se guarda como `string`.
- **Vencimiento atómico:** el `WHERE Status = 1` del `UPDATE` evita pisar una reserva que se confirmó (Tarea 04) justo antes del ciclo: si ya no está `Retained`, el `UPDATE` no la toca.
- **`PeriodicTimer` en lugar de `Task.Delay`:** mantiene el intervalo fijo aunque un ciclo tarde, y se cancela limpio con el `stoppingToken`.

## Dependencias con otras tareas

- **Tarea 01:** publica `ReservationRequestedEvent`. Hoy usa `FakeLoggingPublisher`, así que no llegan mensajes reales; para probar se envían a mano.
- **Tarea 02:** aporta el modelo `Reservation`, `BookingDbContext` e `IBookingRepository`.

## Registro

### 2026-10-29

- **IA:** trajo los cambios de la Tarea 02 a la rama local (fast-forward a `eb956ff`: esquema SQLite, `BookingRepository` e inyección de dependencias). Solo local, sin push.
- **IA:** revisó el código existente y detectó que falta `ConnectionStrings:BookingDataBase` en la configuración de Booking.
- **IA:** creó esta bitácora.
- **Responsable:** compartió el ejemplo de mensajería del docente (`EjemploMensajeria`, proyecto `Mensajeria`).
- **Responsable:** instaló `Azure.Messaging.ServiceBus` 7.21.0 desde Visual Studio (Administrar paquetes NuGet).
- **IA:** verificó la referencia en `Booking.Api.csproj` y que `dotnet build` compila sin errores ni advertencias.
- **Responsable:** cargó el connection string en User Secrets desde Visual Studio (Administrar secretos de usuario). Se agregó `UserSecretsId` al `.csproj`.
- **IA:** verificó, sin mostrar la clave, que existe `ServiceBus:ConnectionString` con endpoint `grupo2`, `SharedAccessKey` y `EntityPath=grupo2-booking-topic`, y que ningún archivo con la clave quedó en el repositorio.
- **Responsable:** agregó `ServiceBus:TopicName` (`grupo2-booking-topic`), `ServiceBus:SubscriptionName` (`booking`) y `ConnectionStrings:BookingDataBase` (`Data Source=booking.db`) en `appsettings.json`. Agregó la regla `*.db` al `.gitignore` para no subir la base SQLite.
- **IA:** verificó que `appsettings.json` es JSON válido y que git ignora `booking.db`.
- **IA:** explicó qué es un `BackgroundService` y pasó el código del esqueleto de `BookingQueueConsumer`.
- **Responsable:** creó la carpeta `Workers` y el archivo `BookingQueueConsumer.cs` desde Visual Studio, y escribió el esqueleto.
- **IA:** verificó que `dotnet build` compila sin errores ni advertencias.
- **IA:** explicó el esqueleto escena por escena (contratación, arranque, mensaje, error, apagado) y el concepto de inyección de dependencias.
- **Responsable:** registró en `Program.cs` el `ServiceBusClient` como Singleton (lee `ServiceBus:ConnectionString` de User Secrets) y el worker con `AddHostedService<BookingQueueConsumer>()`.
- **IA:** verificó el registro y que `dotnet build` compila sin errores ni advertencias.
- **IA:** explicó cómo probar el esqueleto: correr Booking, enviar un mensaje con el proyecto de ejemplo del docente (comentando su bloque de lectura para que no compita por la suscripción `booking`) y ver `Mensaje recibido: ...` en la consola.
- **Pausa:** la sesión se cortó antes de la prueba. Nada commiteado todavía.

### 2026-10-30

- **Responsable:** envió un mensaje de prueba desde el ejemplo.
- **Prueba del esqueleto OK:** Booking registró `Mensaje recibido: Order #1001 created` dos veces. El primer mensaje era el del envío anterior, que había quedado en la suscripción mientras Booking no podía autenticarse.
- **Responsable:** commiteó el esqueleto (`304848f feat(booking): add Service Bus topic consumer skeleton.`).
- **Prompt (textual):** "bien vamos con el paso procesar el mensaje (deserializar ReservationRequestedEvent y registrar la reserva en la base). recuerda explicarme que hace y como funciona esto."
  - **Respuesta IA (resumen):** no pudo dar el paso 5b en esta sesión. Recomendó retomarlo en una sesión nueva.
- **Prompt (textual):** "bien vamos con el paso procesar el mensaje (deserializar ReservationRequestedEvent y registrar la reserva en la base). recuerda explicarme que hace y como funciona esto."
  - **Respuesta IA (resumen):** explicó por qué un hosted service (Singleton) no puede recibir el repositorio (Scoped) y por qué se crea un scope por mensaje con `IServiceScopeFactory`. Explicó el flujo del mensaje: deserializar (JSON inválido → dead-letter), idempotencia por `RequestId`, asiento ocupado → `Rejected`/`SeatOccupied`, libre → `Retained` por 5 min, `DbUpdateException` por carrera → abandon y reintento, y `Complete` al final. Pasó el código de `HandleMessageAsync` para que lo escriba el responsable.
- **Prompt (textual):** "pero no explicaste nada del codigo solo me dijiste que hace."
  - **Respuesta IA (resumen):** reconoció que solo había explicado el flujo. Explicó el código bloque por bloque: `using`, campos `static readonly` (`TimeSpan`, `JsonSerializerDefaults.Web`), constructor con `IServiceScopeFactory`, `BinaryData.ToObjectFromJson` y `catch (JsonException)`, dead-letter, `CreateScope` + `GetRequiredService` + `using var`, idempotencia, object initializer y ternarios, `DbUpdateException` por el índice único + abandon, y `CompleteMessageAsync` al final por `AutoCompleteMessages = false`.
- **Prompt (textual):** "perfecto, ahora me quedo mas claro ya lo transcribi al codigo"
  - **Respuesta IA (resumen):** revisó `BookingQueueConsumer.cs`: la lógica coincide con lo explicado y `dotnet build` compila sin errores ni advertencias. Señaló detalles menores: "se reintegra" en lugar de "se reintenta" (comentario y log), "ReservationRequestEvent" sin la "ed" en la descripción del dead-letter, "instacia" e indentación mezclada (se arregla con Ctrl+K, Ctrl+D en Visual Studio). Próximo: paso 7 (`dotnet ef database update`).
- **Responsable:** instaló la herramienta `dotnet-ef` y creó la base con `dotnet ef database update`.
- **Prompt (textual):** "listo entonces sigamos con el paso 7, ya cree la db con dotnet ef database update"
  - **Respuesta IA (resumen):** verificó que `booking.db` tiene la tabla `Reservations`, los índices `IX_Reservations_RequestId` e `IX_Reservations_EventId_SeatNumber`, y la migración `InitialBookingSchema` aplicada; git ignora el archivo. Explicó cómo correr Booking y armó cuatro pruebas para enviar desde el ejemplo del docente: reserva nueva (`Retained`), mensaje repetido (idempotencia), mismo asiento con otro `ReservationId` (`Rejected`) y texto plano (dead-letter).
- **Prompt (textual):** "listo te mando los resultados de las pruebas: 1. Retained 2. ya procesada se descarta 3. rejected 4. json invalido"
  - **Respuesta IA (resumen):** confirmó en `booking.db` dos filas: `RequestId 1111...` en `Retained` (1) con `ExpiresAtUtc` = creación + 5 min, y `RequestId 2222...` en `Rejected` (3) con motivo `SeatOccupied` (1) y sin vencimiento. El duplicado no generó una tercera fila. Las cuatro pruebas pasaron. Recomendó commitear el paso 5b.
- **Responsable:** commiteó el procesamiento del mensaje (`3ea97c8 feat(booking): process reservation requests from Service Bus`).
- **IA:** armó el plan del trabajo 2 en esta bitácora.

- **Prompt (textual):** "ahora, vamo con  agregar al repositorio el método ExpireOverdueReservationsAsync(DateTime nowUtc, CancellationToken). Usa ExecuteUpdateAsync de EF Core: un solo UPDATE ... SET Status = 4 WHERE Status = 1 AND ExpiresAtUtc <= now directo en la base, sin cargar las reservas en memoria. Devuelve la cantidad de reservas vencidas, para registrarla en el log."
  - **Respuesta IA (resumen):** explicó la diferencia entre el camino clásico (cargar, modificar, `SaveChangesAsync`) y `ExecuteUpdateAsync` (un `UPDATE` directo, sin change tracker). Pasó el código de la firma en `IBookingRepository` y de la implementación en `BookingRepository`, y lo explicó línea por línea: `Where` como filtro SQL, comparación con `DateTime?`, `SetProperty`, el `int` devuelto y el SQL generado. Señaló que SQLite guarda las fechas como texto y que la comparación funciona porque EF usa un formato ordenable.
- **Responsable:** agregó `ExpireOverdueReservationsAsync` en `IBookingRepository` y `BookingRepository`.
- **Prompt (textual):** "listo creado el metodo en el repository"
  - **Respuesta IA (resumen):** revisó los dos archivos: la firma y la implementación coinciden con lo explicado, y `dotnet build` compila sin errores ni advertencias. Sugirió agregar el comentario arriba del método, como tienen los otros métodos del repositorio. Próximo: paso 2 (`ReservationExpiration:IntervalSeconds` en `appsettings.json`).
- **Responsable:** agregó la sección `ReservationExpiration` con `IntervalSeconds: 15` en `appsettings.json`.
- **Prompt (textual):** "listo ya esta el intervalo vamos con Workers/ReservationExpirationWorker.cs. BackgroundService con un PeriodicTimer que se dispara cada IntervalSeconds. Un scope por ciclo con IServiceScopeFactory, igual que en el consumer. try/catch por ciclo: si un ciclo falla (por ejemplo, SQLite bloqueada), se registra el error y el worker sigue con el próximo."
  - **Respuesta IA (resumen):** verificó que `appsettings.json` es JSON válido y tiene `ReservationExpiration:IntervalSeconds = 15`. Explicó el paso 3 en orden (qué, por qué, dónde, código y línea por línea): lectura del intervalo con `GetValue` y valor por defecto, `PeriodicTimer` con `do/while` para correr un ciclo apenas arranca, scope por ciclo, `catch (Exception) when (...)` para que un error no tire abajo toda la aplicación, y log solo cuando se vence alguna reserva.
- **Responsable:** creó `Workers/ReservationExpirationWorker.cs`.
- **Prompt (textual):** "listo ya cree el worker, continuamos con registrar el worker en Program.cs con AddHostedService<ReservationExpirationWorker>()."
  - **Respuesta IA (resumen):** revisó el worker: coincide con lo explicado, sin doble extensión, y `dotnet build` compila sin errores ni advertencias. Explicó el paso 4 (qué, por qué, dónde, código y línea por línea): `AddHostedService` registra el worker como Singleton y el host llama a su `ExecuteAsync` al arrancar; va junto al registro del consumer y no necesita un nuevo `using` porque `Booking.Api.Workers` ya está importado.
- **Responsable:** registró el worker en `Program.cs`, compiló y corrió Booking.
- **Prompt (textual):** "listo, agregado ya lo buildee y lo runee"
  - **Respuesta IA (resumen):** verificó el registro en `Program.cs`. En la consola se ve el `UPDATE` que genera `ExecuteUpdateAsync` y el log `Se vencieron 1 reservas`. En `booking.db`, la reserva `1111...` pasó de `Retained` (1) a `Expired` (4) y la `2222...` (`Rejected`) no se tocó. Prueba 6a OK. Próximo: pruebas 6b y 6c.
- **Prompt (textual):** "perfecto paso las dos pruebas"
  - **Respuesta IA (resumen):** confirmó en `booking.db`: `3333...` (A2) pasó a `Expired` (4) al cumplirse los 5 minutos, y `4444...` (A2) quedó `Retained` (1), o sea que el asiento se liberó al vencer (trabajo 3 verificado). Detectó que git muestra `booking.db-shm` y `booking.db-wal` como archivos nuevos (los arma SQLite en modo WAL) y recomendó ignorarlos en `.gitignore` antes de commitear.
