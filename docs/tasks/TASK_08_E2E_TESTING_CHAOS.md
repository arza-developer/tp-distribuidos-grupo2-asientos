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


## Prompts

Promp: Podrias ayudarme y explicarme a realizar los test e2e en esta carpeta?
Respuesta: Revisé la carpeta nueva. Es un clon limpio del repo, en tu rama feature/08-e2e-testing-diagram

El connection string del broker

El sistema usa Azure Service Bus, el broker de la cátedra. Sin la clave, Availability responde 503 al reservar.

Pedile al grupo el valor de SERVICEBUS_CONNECTION_STRING. Lo tiene quien hizo Availability o Booking.
Creá un archivo llamado .env en la raíz del repo, al lado del docker-compose.yml, con una línea:
SERVICEBUS_CONNECTION_STRING=<el valor que te pasen>
Fijate que .env esté en el .gitignore para que nunca se suba. Es una clave secreta.

Fase 2: Aprender el contrato real (antes de escribir tests)

No escribas nada todavía. Abrí y leé este archivo:

src\Services\Availability\Availability\scenarios\availability.http

Lo hizo tu compañero de Availability y es tu mejor guía, porque muestra cómo se llama de verdad a cada endpoint. Lo importante:

Cosa	Cómo es en realidad
Login	POST :5104/connect/token, formulario con grant_type=password, cliente1 / cliente1-dev
Evento	Siempre 3fa85f64-5717-4562-b3fc-2c963f66afa6, que es el único cargado en la base
Asientos libres	A-1, A-2, B-1 (A-3 está ocupado de entrada)
Estados	En inglés: Pending → Retained → Confirmed, o Rejected / Expired
Respuesta del estado	Campos status, message, seatNumber, expiresAtUtc

Para practicar, podés abrir ese archivo en VS Code con la extensión httpYac y ejecutar cada pedido con el botón “Send” que aparece arriba. Ves la respuesta al instante.

Fase 3: Escribir vos el happy path en tests\api-tests.http

Primero, lo mínimo que necesitás saber de la sintaxis de httpyac:

@nombre = valor arriba de todo define una variable, y la usás como {{nombre}}.
### separa un pedido del siguiente.
# @name login le pone nombre a un pedido. Después usás su respuesta con {{login.access_token}}.
?? status == 200 es una verificación: si no se cumple, el test falla. Para campos del JSON se usa ?? js response.parsedBody.status == Confirmed.
# @sleep 5000 espera 5 segundos antes de mandar ese pedido.

Armalo en este orden, de a un pedido, corriendo el archivo cada vez que agregás uno:

Login. Verificá status 200 y que venga access_token. Esto prueba la autenticación.
Catálogo sin token. Verificá 200. Esto prueba que la consulta es pública.
Reserva con token (B-1, por ejemplo). Verificá 202 y que venga reservationId. El 202 demuestra la consistencia eventual: Availability responde antes de que Booking decida.
Estado de la reserva, con un @sleep antes. Verificá que status sea Retained o Confirmed. Esto prueba que el mensaje viajó por el broker y Booking lo procesó.
Reserva sin token. Verificá 401.

Prompt: me explicas nuevamente lo del login?
Respuesta:
Te lo explico más despacio, con el texto completo a la vista.
Qué estamos haciendo

Para reservar, el sistema te pide estar logueado. En este sistema “loguearse” es pedirle un token al Auth Server: le mandás usuario y contraseña, y te devuelve un access_token, un texto largo que funciona como una entrada al recital. Después, en cada pedido protegido, mostrás esa entrada. El primer test comprueba que el login funciona y te da ese token.
Crea una variable llamada authUrl. Donde después escribas {{authUrl}}, httpyac la reemplaza por http://localhost:5104, que es donde corre el Auth Server. Las otras tres son iguales: la dirección de Availability, el ID del evento cargado en la base y el ID del usuario cliente1. Todavía no usás esas tres, pero las vas a necesitar para la reserva.

Prompt: Me faltan que arranquen 2 servicios, puede ser un tema que otro este usando el puerto?
Respuesta: El login anduvo, pero el catálogo y la reserva dan RequestError en localhost:5101: nadie atendió en ese puerto. Después del docker compose down -v + up -d, Availability tarda más en arrancar, porque recrea su base y carga los asientos. Seguramente corriste los tests antes de que terminara. También puede que se haya caído.
Revisalo con:
docker ps
docker compose logs availability --tail 30

Mirá todos los contenedores, también los frenados:
docker ps -a

Volvé a levantar y leé el final:
docker compose up -d

Si es el puerto, buscá quién lo usa:
netstat -ano | findstr ":5103 :5101"
netsh interface ipv4 show excludedportrange protocol=tcp

Si aparece algo, anotá el PID (el número de la última columna) y fijate qué programa es:
tasklist /FI "PID eq <PID>"

Pasame la salida de los tres comandos. Según cuál sea el caso, la solución es distinta: cerrar un programa o liberar el rango reservado. 

Prompt: Los servicios arrancaron normalmente luego del docker compose up -d

Respuesta:
El problema del puerto fue momentáneo. Cuando hacés docker compose down, Windows a veces tarda unos segundos en liberar los puertos, y si el up llega antes, choca.

Para la próxima: si después de un down algún contenedor queda en Created, corré docker compose up -d otra vez y listo.