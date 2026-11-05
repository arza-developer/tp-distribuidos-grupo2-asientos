# ADR-007: Seguridad, Autenticación M2M y Validación de Tokens

**Estado:** Aprobado  
**Fecha:** 2026-10-28  

## 1. Contexto y Restricciones
- Requisito 3.e: los microservicios deben autenticarse entre sí mediante el servidor de autorización.
- El catálogo público permite navegación anónima; las operaciones transaccionales exigen tokens válidos.

## 2. Alternativas Evaluadas

### Opción A: Introspección Sincrónica en Cada Solicitud (RFC 7662)
- Cada microservicio consulta a `AuthServer` vía HTTP para validar cada token recibido.
- Desventajas: convierte al servidor de identidad en un cuello de botella crítico y duplica la latencia de red en cada salto.

### Opción B: Criptografía Asimétrica (RS256 con JWKS)
- Tokens firmados con clave privada y verificados con clave pública descargada por los servicios.
- Desventajas: sobrecarga operativa para rotación y distribución de llaves dentro de un entorno Docker local.

### Opción C: Firma Simétrica HS256 con Validación Local y Cliente Dedicado (Seleccionada)
- Clave secreta compartida (`Jwt:Key`) entre los servicios internos, con validación en memoria y cliente HTTP optimizado.

## 3. Decisión
1. **Emisión Centralizada:** `AuthServer.Api` (:5104) emite tokens JWT simétricos (HS256).
   - Flujo de usuarios: `password` y `authorization_code` con PKCE.
   - Flujo interservicio (M2M): `client_credentials` con credenciales individuales por servicio (`availability`, `booking-api`).
2. **Validación Descentralizada:** cada servicio receptor valida localmente en memoria firma, vigencia, emisor (`Issuer`) y audiencia (`Audience`), sin llamadas de red adicionales.
3. **Optimización del Cliente M2M:** en `Booking.Api`, la obtención de tokens mediante `AuthenticationClient` implementa:
   - Control de concurrencia mediante `SemaphoreSlim(1,1)` para evitar ráfagas de solicitudes simultáneas al endpoint `/connect/token`.
   - Almacenamiento en memoria del token y cálculo de renovación anticipada (`refreshMargin`), reutilizándolo mientras sea válido.

## 4. Costo de Mantenimiento y Mitigaciones
- **Distribución de Clave Secreta:** todos los servicios internos que validan tokens conocen la clave simétrica.
  *Mitigación:* la clave se inyecta mediante variables de entorno en la red interna de Docker. El IdP externo simulado opera con claves independientes y no tiene acceso al secreto interno.
