# Historial de prompts — Matías Raffo

**Tarea:** 06 — AuthServer (OAuth2 y autenticación M2M)
**Rama:** `feature/06-auth-server-m2m`
**Herramienta:** Claude (Anthropic)
**Consigna:** paso 2 del flujo (el usuario obtiene un token) y punto 3.e (los microservicios se autentican entre sí con el servidor de autorización).

## Cómo usé la IA

La usé como tutor, no para que me escribiera todo de una vez. Fuimos paso a paso: primero entender cada concepto y después implementarlo. Cada paso lo escribí, compilé y probé yo (con el archivo `.http`, verificando los 401 y 200) antes de pasar al siguiente. Las decisiones las contrasté con el material de la materia, sobre todo la Clase 12 (Autenticación y autorización).

---

## 1. Entender los conceptos

**Prompt:**
> Explicame qué es un claim y cómo funciona un token JWT, de forma simple.

**Resultado:** entendí qué es un token, qué son los claims (`sub`, `role`, `iss`, `aud`, `exp`) y para qué sirve la firma, con una analogía (pulsera de acceso con holograma).

---

## 2. Analizar el repositorio y mi tarea

**Prompt:**
> Te paso la consigna, mi especificación (Tarea 06) y el repositorio del grupo. Explicame cómo está organizado, con qué servicios me tengo que conectar y qué le falta a mi parte.

**Resultado:** detectamos cuatro problemas en el AuthServer original: no validaba el `client_secret`, el rol venía del pedido (cualquiera podía pedir rol Admin), la clave estaba escrita en el código, y no existía el método `AddJwtAuthentication` para el resto de los servicios.

---

## 3. Implementar el AuthServer

**Prompt:**
> Hagamos la implementación juntos, paso a paso, para que vaya entendiendo cada parte.

**Resultado:**
- La clave pasó a configuración (`appsettings.Development.json`) y, en Docker, a una variable de entorno compartida.
- Lista de clientes y usuarios en `appsettings.json`, con el patrón Options (`AuthServerOptions`). El rol lo decide el servidor.
- `TokenService` separado del endpoint `POST /connect/token`, con el flujo **Client Credentials** para los microservicios (requisito 3.e).
- Librería compartida `SeatsReservation.Auth` con `AddJwtAuthentication`, para que todos los servicios validen los tokens igual.
- Pruebas en `AuthServer.Api.http`: 401 sin token, 200 con token válido, 401 con token falsificado.

---

## 4. Entender la validación local y Docker

**Prompt:**
> No termino de entender por qué los servicios validan el token sin consultar al AuthServer, ni qué rol cumple Docker en mi parte.

**Resultado:** entendí que cada servicio recalcula la firma con la clave compartida (no necesita llamar al AuthServer) y por qué la clave tiene que ser la misma en todos los contenedores. En Docker: la clave se define una sola vez en el `docker-compose.yml`, y los servicios se encuentran por nombre (`auth-server`), no por `localhost`.

---

## 5. Validación por un tercero

**Prompt:**
> Un compañero me marcó que falta la validación por un tercero (3rd Party Identity Providers del diagrama). ¿Qué opciones hay? ¿Cuál es la más fácil de configurar, explicar y usar?

**Resultado:** comparamos Keycloak, Google/GitHub, mock-oauth2-server, Dex y un simulador propio. Elegimos un **proveedor externo simulado** (`ExternalIdentityProvider.Api`) que firma con **RS256** y publica su clave pública en **JWKS**. El AuthServer verifica ese `id_token` y lo canjea por su propio token (**Token Exchange**). Se simula igual que Azurite simula el broker.

---

## 6. Alinear con lo visto en clase

**Prompt:**
> En clase vimos Authorization Code con PKCE, Client Credentials, entre otros. ¿Podemos usar alguno de estos flujos para la autenticación con terceros?

**Resultado:** cada flujo tiene su uso. **Client Credentials** es para microservicios (ya lo teníamos para el requisito 3.e). **Authorization Code + PKCE** es el que se usa para el login de personas, y es también el que usan los proveedores reales como Google en el "Iniciar sesión con…". Lo aplicamos al login de usuarios del AuthServer (punto 7). Además, el grant `password` que usábamos no está entre los flujos vigentes de la Clase 12, así que lo dejamos solo para pruebas.

---

## 7. Login de usuarios con Authorization Code + PKCE

**Prompt:**
> El usuario debería poder loguearse sin que yo mande los pedidos a mano. Vamos con Authorization Code + PKCE, que es lo que vimos en clase.

**Resultado:**
- Página de login con **Razor Pages** en `/connect/authorize`: el usuario escribe su contraseña en el AuthServer y el front nunca la ve.
- Códigos de autorización de un solo uso que vencen en 60 segundos (`AuthorizationCodeStore`).
- Canje del código verificando **PKCE** (`code_verifier` contra `code_challenge` con SHA256).
- Pruebas: canje correcto (200), código reutilizado (400), código robado sin el verificador correcto (400).

---

## 8. Login con el proveedor externo, como "Continuar con Google"

**Prompt:**
> Un compañero pidió un mínimo front en el proveedor de identidad externo, para simular un login como el de Google (por ejemplo, en GitHub podés entrar con Google y te abre otra página). Hagamos lo que mejor quede y no sea tan complicado.

**Resultado:**
- El proveedor externo tiene su propia **página de login** (Razor Pages). La persona escribe su contraseña ahí, no en nuestra app.
- En la página de login del AuthServer agregamos el botón **"Ingresar con Proveedor Externo"**.
- El proveedor devuelve el `id_token` a `/external/callback` con **OIDC form_post**. Se valida con `state` (que el login lo empezamos nosotros) y `nonce` (que el token sea para este login).
- Después sigue el mismo Authorization Code + PKCE, así que para el front no cambia nada.
- Pruebas, también en Docker: Ana entra con su ID de nuestro sistema; Juan, sin vincular, ve una página de error.

---

## 9. Integración con el front y con el resto del grupo

**Prompt:**
> Repasemos el repositorio. ¿Cómo se conecta el login del front de Availability con el AuthServer?

**Resultado:**
- El front de Availability usaba el grant `password`, así que no pasaba por la página de login ni por el proveedor externo. Agregamos el parámetro `idp_hint=external` para que el botón "Ingresar con Proveedor Externo" del front lleve directo a la página del proveedor, y registramos su dirección de vuelta (`/Account/Callback`). El compañero de Availability implementó su lado con PKCE.
- Probamos el recorrido completo desde el front: login, reserva y estado, con Availability → Booking → Payment usando tokens de servicio (Client Credentials).
- Al revisar `develop` detectamos que, al integrar la observabilidad, `client_credentials` quedó firmando con una clave inexistente (`Jwt:SecretKey`) y todos los tokens M2M se rechazaban. Lo avisamos al grupo y se corrigió volviendo a usar `TokenService`.

