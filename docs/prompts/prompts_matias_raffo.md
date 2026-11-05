# Historial de Prompts - Matías Raffo
**Rol:** Tarea 06 - Servidor de Autorización OAuth2, Tokens M2M y Login Externo  
**Rama:** `feature/06-auth-server-m2m`

### Contexto y Desafíos Reales
Implementación del endpoint unificado `/connect/token`, soporte M2M para microservicios y resolución del proveedor de identidad federado.

### Prompts Clave Utilizados

#### Prompt 1: Endpoint OAuth2 con Form-UrlEncoded en Minimal APIs
> "Tengo que implementar `POST /connect/token` soportando `grant_type=password` y `grant_type=client_credentials` según RFC 6749. Cuando mando la petición desde Postman o curl con `application/x-www-form-urlencoded` me tira error 415. ¿Cómo bindeo parámetros de formulario en Minimal APIs de ASP.NET Core 8?"

#### Prompt 2: Fix de Desajuste de Clave JWT (Jwt:Key vs Jwt:SecretKey)
> "En el chat vimos que Booking no podía validar los tokens M2M porque AuthServer estaba leyendo la clave desde `Jwt:SecretKey` y en `docker-compose.yml` la variable compartida se llama `Jwt:Key`. ¿Cómo unifico la lectura de la clave simétrica HS256 para firmar el token y cómo configuro los claims `role` y `sub`?"

#### Prompt 3: Simulación de Proveedor Externo (Alternativa a Keycloak)
> "Facundo me comentó en WhatsApp que la cátedra pide login por terceros (Google). Averigüé de usar Keycloak localmente pero es pesadísimo y consume mucha memoria en Docker. ¿Cómo puedo armar un microservicio liviano en ASP.NET Core (`ExternalIdentityProvider.Api`) que simule un flujo OIDC con Authorization Code + PKCE para no complicar el despliegue del TP?"
