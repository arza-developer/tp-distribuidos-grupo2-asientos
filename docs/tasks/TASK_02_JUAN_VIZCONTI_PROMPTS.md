# Registro de uso de inteligencia artificial — Tarea 02: Booking Core y concurrencia

**Trabajo práctico:** Sistema distribuido de reserva de asientos — Programación IV  
**Módulo desarrollado:** Booking Core (Tarea 02)  
**Repositorio:** https://github.com/arza-developer/tp-distribuidos-grupo2-asientos  
**Rama de trabajo:** `feature/02-booking-core-concurrency`  
**Herramienta de apoyo:** ChatGPT (OpenAI)  
**Período abordado:** octubre–noviembre de 2026  
**Fecha de elaboración del registro:** 3 de noviembre de 2026

## 1. Objetivo y alcance

Este documento registra una **selección de prompts relevantes** realizados al asistente de IA durante el desarrollo de Booking Core. Se incluyen preguntas técnicas, decisiones de diseño, depuración, integración con otras tareas y validación mediante pruebas automatizadas.

**Importante:** este archivo es un **registro resumido de interacciones significativas**, no una exportación íntegra ni literal de todas las respuestas de la conversación. Los textos entre comillas son extractos de mensajes del estudiante; cuando se acortó un mensaje extenso se indica mediante `[…]`. Las columnas «Orientación del asistente» y «Aplicación / decisión» son resúmenes elaborados para esta entrega, no citas textuales de ChatGPT.

El uso de IA fue de apoyo para comprender conceptos, comparar alternativas, detectar problemas, proponer pruebas y orientar la integración. El estudiante tomó decisiones de arquitectura, implementó y modificó código en Visual Studio, resolvió conflictos de Git y ejecutó las pruebas en su entorno local. La evidencia concreta de autoría y cambios se encuentra en el historial de commits y el Pull Request del repositorio.

## 2. Prompts clave y su aporte al desarrollo

### Etapa A — Concurrencia, idempotencia y depuración

**01. Diferenciar concurrencia de reentrega de una misma solicitud**

> «porque queremos comprobar la condicion de carrera entre dos solicitudes distintas, no queremos comprobar la recuperacion del procesamiento de la reqeust mediante otra instancia, genera los test vos»

**Orientación del asistente:** propuso pruebas xUnit sobre SQLite para asiento libre, asiento ocupado, `RequestId` repetido y diez solicitudes simultáneas. Explicó que dos solicitudes distintas deben compartir `EventId` y `SeatNumber`, pero tener diferentes `RequestId`.

**Aplicación / decisión:** se incorporaron pruebas de concurrencia e idempotencia en `Booking.Tests`, usando contextos independientes para la prueba simultánea.

**02. Diagnóstico de una falla por índice único**

> «[Captura del Explorador de pruebas: tres pruebas correctas y la de diez solicitudes concurrentes fallida]»

**Orientación del asistente:** analizó el error `SQLite Error 19: UNIQUE constraint failed: Reservations.EventId, Reservations.SeatNumber`; distinguió la protección del índice único de la recuperación del conflicto por parte del servicio.

**Aplicación / decisión:** se revisó el bloque `catch (ReservationUniqueConflictException)` de `TryRetainReservationAsync()` para recuperar resultados por `RequestId` o registrar un rechazo por asiento ocupado.

**03. Resolver un error de ámbito de variables en C#**

> «yo tenia implementado el codigo distinto, porque cuando hacia el `var existing = await _repository.GetByRequestIdAsync(request.RequestId);` dentro del catch me da un error, por eso me saltie esa consulta […]»

**Orientación del asistente:** explicó el error `CS0136` al redeclarar nombres de variables existentes en un ámbito exterior y sugirió nombres distintos (`existingByRequestId`, `seatOccupiedNow`).

**Aplicación / decisión:** se mantuvieron las comprobaciones de `RequestId` y de asiento activo dentro del manejo del conflicto. Posteriormente pasaron las cuatro pruebas iniciales.

**04. Confirmación idempotente**

> «considerar que la confirmacion fue exitosa, deberiamos recuperar ese estado al igual que hicimos con reservation. listo commit ya hecho»

**Orientación del asistente:** propuso una transición atómica `Retained → Confirmed` mediante `ExecuteUpdateAsync`, recuperando el estado persistido. Si ya estaba `Confirmed`, el servicio debía devolver la reserva existente.

**Aplicación / decisión:** se implementaron `ConfirmIfValidAsync()` y `ConfirmReservationAsync()`, con pruebas de confirmación repetida.

**05. Rechazos idempotentes con motivo consistente**

> «deberiamos comprobar la razon de rechazo, y verificar que coincida con nuestra razon»

**Orientación del asistente:** recomendó considerar idempotente un segundo rechazo solo cuando el estado sea `Rejected` y el `RejectionReason` coincida; un motivo distinto representa un conflicto.

**Aplicación / decisión:** se implementó `RejectReservationAsync()` junto con pruebas para motivo coincidente, motivo distinto y rechazo de una reserva ya confirmada.

### Etapa B — Vencimiento y seguridad de transiciones

**06. Condición de pago síncrono aclarada por el profesor**

> «[…] obtuve respuesta de mi profesor sobre la duda que teniamos de que pasa si la confirmacion de un pago llega despues de vencer una reserva, la llamada al servicio de payment tiene que ser sincrona, por lo tanto debemos esperar la respuesta de payment y por el plazo que pusimos de 10 minutos es dificil que se esta situacion»

**Orientación del asistente:** distinguió «esperar la respuesta dentro del flujo de negocio» de bloquear un hilo en .NET; recomendó conservar las validaciones temporales pese a la baja probabilidad de una respuesta tardía.

**Aplicación / decisión:** se mantuvo la condición de vencimiento en la confirmación de la reserva.

**07. Reserva vencida en tiempo pero todavía retenida en la base**

> «[…] esa segunda solicitud, no podra reservar el asiento porque su estado sigue siento retained aunque haya superado su tiempo de expiracion»

**Orientación del asistente:** explicó que el índice único parcial continúa bloqueando el asiento hasta que el Worker actualiza el estado de `Retained` a `Expired`.

**Aplicación / decisión:** se dejó la liberación periódica de reservas a cargo del Worker; la expiración bajo demanda quedó fuera del alcance inmediato.

**08. Condiciones atómicas frente a confirmación y expiración simultáneas**

> «por las validaciones que aplicamos en cada uno de los metodos, si intentamos confirmar una reserva no puede estar en estado vencida, y si intentamos vencer un reserva no puede estar confirmada»

**Orientación del asistente:** profundizó en por qué las condiciones en un `UPDATE` atómico son más seguras que leer el estado y luego guardar; propuso una prueba concurrente entre confirmación y expiración.

**Aplicación / decisión:** se agregó una prueba con contextos de base de datos independientes para comprobar que una reserva vencida no termina confirmada.

### Etapa C — Integración con Booking Worker (Tarea 03)

**09. Identificar el método que debe invocar el consumidor de mensajes**

> «antes de abordar como implementar payment, quiero preguntarte algo, cuando el worker traiga un mensaje del message broker, Que metodo llamara de nuestra implementacion y como seria el flujo hasta que nosotros llamamos a payment»

**Orientación del asistente:** explicó la secuencia `BookingQueueConsumer → IBookingService → retención → Payment → confirmación/rechazo` y propuso un método orquestador independiente, `ProcessBookingAsync()`.

**Aplicación / decisión:** se definió que el Worker delegaría el procesamiento completo en Booking Core.

**10. Responsabilidad de Booking respecto de Payment**

> «me parece bien que el booking core sea el encargado de llamar a payment»

**Orientación del asistente:** explicó la separación de responsabilidades: Worker recibe y gestiona mensajes; Booking Core orquesta; el cliente Payment encapsula gRPC.

**Aplicación / decisión:** se decidió no distribuir la lógica de negocio entre el Worker y el servicio de pagos.

**11. Revisar el código recibido de BookingQueueConsumer**

> «[Código compartido de `BookingQueueConsumer`, incluyendo `CreateScope`, `IBookingService`, `TryRetainReservationAsync` y `CompleteMessageAsync`]»

**Orientación del asistente:** revisó el alcance de `DbContext` por mensaje, la confirmación manual del mensaje en Azure Service Bus, los mensajes inválidos y el futuro cambio hacia `ProcessBookingAsync()`.

**Aplicación / decisión:** se conservó el consumo mediante `IBookingService` y se planificó modificar el punto de entrada del Worker para incluir el procesamiento del pago.

**12. Unificar la expiración con la implementación de la tarea 03**

> «mi compañero hizo su propio metodo de expiracion, decidimos usar el suyo asique el nuestro hay que eliminarlo»

**Orientación del asistente:** comparó la expiración individual con `ExpireOverdueReservationsAsync()` y destacó la actualización masiva atómica, el filtrado por estado `Retained` y la devolución de la cantidad de filas modificadas.

**Aplicación / decisión:** se eliminaron los métodos de vencimiento individual y se conservaron los métodos de expiración del compañero.

**13. Adaptar las pruebas al nuevo método de vencimiento**

> «ya elimine todo los metodos relacionado con la vieja implementacion de expiracion, ahora voy a pasarte el contenido del booking.core test y decime donde modificarlo»

**Orientación del asistente:** indicó las cuatro pruebas que debían modificarse y propuso una prueba nueva para reservas retenidas aún vigentes.

**Aplicación / decisión:** se actualizaron las pruebas para consultar el estado en SQLite después de `ExpireOverdueReservationsAsync()`. La ejecución posterior informó **12 pruebas correctas**.

### Etapa D — Contratos e integración con Payment (Tareas 04 y 05)

**14. Revisar el evento publicado por Availability**

> «[Código compartido de `ReservationRequestedEvent` con `ReservationId`, `EventId`, `SeatNumber`, `UserId`, `Price` y `CorrelationId`]»

**Orientación del asistente:** detectó que el precio ya llega en el evento y recomendó pasarlo como argumento independiente del DTO de retención.

**Aplicación / decisión:** se definió `ProcessBookingAsync(ReserveSeatRequest request, decimal amount, ...)`.

**15. Revisar la idempotencia del servicio de Payment**

> «el correlationId que llega de availability vendria a ser nuestro requestId […] `PaymentService.cs` [código del método `ProcessPayment`]»

**Orientación del asistente:** analizó la consulta de transacciones por `CorrelationId`, la diferencia entre reintentos secuenciales y concurrentes, y el riesgo de confundir trazabilidad con clave de idempotencia.

**Aplicación / decisión:** se eligió enviar un identificador estable a Payment para evitar repetir el cobro lógico.

**16. Investigar qué identificadores genera realmente Availability**

> «me gustaria encontrar la parte del codigo en la que availability genera la peticion, para que recibe verdaderamente, el worker de booking»

**Orientación del asistente:** sugirió buscar `ReservationRequestedEvent` y seguir la creación y publicación del mensaje desde `ReservationsController`.

**Aplicación / decisión:** el estudiante encontró que Availability utiliza `Guid.NewGuid()` para `ReservationId` y `HttpContext.TraceIdentifier` para `CorrelationId`.

**17. Decidir el contrato entre RequestId, ReservationId y CorrelationId**

> «Lo que si quier oresolver es el problema de requestid, reservationid y corelationid. […] cambiaria esta liena \"RequestId = evt.ReservationId\" por \"RequestId=evt.CorrelationId\" […]»

**Orientación del asistente:** comparó separar identificadores de operación, entidad y trazabilidad frente a unificarlos; advirtió que `HttpContext.TraceIdentifier` no necesariamente es un GUID ni una clave estable entre peticiones HTTP diferentes.

**Aplicación / decisión:** se conservó `RequestId = evt.ReservationId` como identificador estable de la solicitud; Booking genera su propio `Reservation.Id` y entrega `RequestId.ToString()` a Payment como clave de idempotencia. No se modificó la base de datos para guardar `CorrelationId`.

**18. Priorizar una integración académica funcional**

> «[…] es un tp academico y no un proyecto que va a ser entregado a un cliente […] primero hagamos que nuestra oarte funcione y se conecte directamente con la de las demas tareas […]»

**Orientación del asistente:** delimitó los cambios mínimos necesarios para el trabajo: orquestador, inyección del cliente Payment y adaptación del consumidor de mensajes, sin refactorizar innecesariamente todos los contratos.

**Aplicación / decisión:** se priorizó la integración funcional y se dejaron mejoras adicionales para una etapa posterior.

**19. Incorporar IPaymentClient a BookingService**

> «listo ahora tenemos que inyectar ipaymentclient en service»

**Orientación del asistente:** indicó cómo agregar `IPaymentClient` al constructor y verificar su registro en `Program.cs`.

**Aplicación / decisión:** se reutilizó el registro `AddScoped<IPaymentClient, PaymentClient>()` aportado por la tarea 05.

**20. Implementar el orquestador antes de actualizar los tests**

> «`builder.Services.AddScoped<IPaymentClient, PaymentClient>();` asi esta payment client en program cs. dejemos los test para el final primero hagamos proccespaymentasync»

**Orientación del asistente:** propuso el método `ProcessBookingAsync()` para retener, esperar el resultado de `ProcessPaymentAsync()`, confirmar ante `Confirmed`, rechazar ante `Failed` y tratar `Unavailable` como resultado incierto.

**Aplicación / decisión:** se implementó el orquestador y se compiló `Booking.Api` antes de adaptar las pruebas.

**21. Conectar el orquestador con el consumidor del broker**

> «todo compila correctamente, ahora voy a modificar la llamada desde el qeue consumer»

**Orientación del asistente:** indicó reemplazar `TryRetainReservationAsync(request, ...)` por `ProcessBookingAsync(request, evt.Price, ...)` y gestionar `PaymentUnavailableException` sin completar el mensaje como exitoso.

**Aplicación / decisión:** el estudiante actualizó `BookingQueueConsumer` y verificó la compilación.

**22. Cerrar con pruebas y Pull Request**

> «todo compila correctamente, tenemos que actualizar los test, luego traer los ultimos cambios de develop y ya podriamos crear el pr»

**Orientación del asistente:** propuso un cliente falso `FakePaymentClient` para no depender del servicio gRPC en xUnit y pruebas para pagos aprobados, rechazados, duplicados y no disponibles.

**Aplicación / decisión:** se ejecutaron **16 pruebas correctamente**, se hizo commit y se preparó la integración final de `develop`.

### Etapa E — Git, integración entre ramas y revisión de código

**23. Elegir entre cherry-pick, merge de una rama y develop**

> «y porque no traemos toda su branch?»  
> «ya varias personas subieron sus cambios a develop, crees que seria mejor traer los cambios de ahi?»

**Orientación del asistente:** comparó las estrategias `cherry-pick` y `merge`; recomendó integrar `develop` en la rama de Booking cuando los PRs de las tareas relacionadas estuvieran incorporados.

**Aplicación / decisión:** se realizaron merges sucesivos de `develop` a `feature/02-booking-core-concurrency`.

**24. Resolver conflictos de archivos de configuración**

> «`git merge origin/develop` […] `CONFLICT (content): Merge conflict in .gitignore` […] `appsettings.json`»

**Orientación del asistente:** explicó los marcadores de conflicto de Git y cómo conservar simultáneamente la conexión SQLite, la configuración del Worker y las reglas de `.gitignore`.

**Aplicación / decisión:** se editaron archivos, validó el JSON y completó el merge con `git merge --continue`.

**25. Resolver el conflicto de la solución tras reorganizar Availability**

> «hice el merge y primero me aparece esto, en el intento anterior aprete recargar y luego me aparecio el error del que te hable»  
> «`get-content SeatsReservation.slnx` [contenido con marcadores `<<<<<<< HEAD` y `>>>>>>> origin/develop`]»

**Orientación del asistente:** explicó que Visual Studio intentaba recargar una solución con conflictos y propuso conservar las referencias tanto a `Booking.Tests` como a los nuevos proyectos de Availability.

**Aplicación / decisión:** se completó el merge y, según el resultado compartido, se compiló `SeatsReservation.slnx` y aprobaron nuevamente las **16 pruebas de Booking**.

## 3. Resultado verificable al cierre de este registro

Según las salidas de terminal compartidas en la conversación:

| Comprobación | Resultado informado |
|---|---|
| Compilación de la solución `SeatsReservation.slnx` | Correcta tras el último merge |
| Pruebas de `Booking.Tests` | **16 correctas, 0 errores** |
| Persistencia | EF Core + SQLite |
| Control de concurrencia | Índice único parcial y actualizaciones SQL condicionales |
| Idempotencia | `RequestId` en Booking y clave estable enviada a Payment |
| Integración con la tarea 03 | Consumidor y vencimiento periódico por `IBookingService` |
| Integración con la tarea 05 | Orquestación mediante `IPaymentClient` |

**Alcance de la validación:** las pruebas descritas son de código y persistencia local (incluyendo tests con SQLite y Payment simulado). **No equivalen a una prueba de extremo a extremo** de Availability → Azure Service Bus → Booking → Payment funcionando simultáneamente. Esa validación se dejó para la integración grupal.

## 4. Declaración de uso responsable de IA

El asistente se utilizó para explicar conceptos de C# y .NET, discutir decisiones de arquitectura, proponer fragmentos de código y pruebas, interpretar errores de Git/SQLite y orientar la integración de microservicios. Las decisiones finales, los cambios de código, los commits y la ejecución local de pruebas fueron realizados por el estudiante y se pueden contrastar con el historial del repositorio.

Este documento selecciona los prompts más relevantes para la consigna académica y procura reflejar tanto las sugerencias aceptadas como las decisiones del estudiante de mantener o descartar cambios. No constituye una transcripción completa de la conversación.
