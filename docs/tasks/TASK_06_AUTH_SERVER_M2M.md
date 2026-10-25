# 📋 Especificación Técnica: Tarea 06
**Rama Git:** `feature/06-auth-server-m2m`  
**Módulo:** `src/Services/AuthServer.Api/`  
**Puerto:** `HTTP 5104`  

## 1. Alcance y Responsabilidad
Centralizar la emisión y validación de credenciales JWT para usuarios y autenticación entre microservicios (M2M).

## 2. Requisitos de Cátedra
- **Protocolo:** OAuth2 / OpenID Connect (Client Credentials Flow).
- **Firma:** Simétrica HMAC-SHA256 (HS256).
- **Requisito 3.e:** Autenticación obligatoria entre microservicios.

## 3. Contratos y Endpoints
- `POST /connect/token`: Retorna `{ access_token, token_type: "Bearer", expires_in }`.

## 4. Tareas Concretas
1. Implementar generación de tokens con claims RFC 7519 (`sub`, `iss`, `aud`, `role`).
2. Suministrar método de extensión `AddJwtAuthentication` para que el resto de los servicios protejan sus endpoints con `[Authorize]`.

## 5. Criterios de Aceptación (DoD)
- [ ] Endpoints protegidos rechazan llamadas sin token con `401 Unauthorized`.
- [ ] Llamadas con token válido emitido por AuthServer devuelven `200/202`.

## 6. Defensa Oral
- **¿Por qué tokens simétricos HS256?**  
  *Respuesta:* Permite validación local ultrarrápida en memoria en cada microservicio sin saturar al AuthServer con consultas remotas.
