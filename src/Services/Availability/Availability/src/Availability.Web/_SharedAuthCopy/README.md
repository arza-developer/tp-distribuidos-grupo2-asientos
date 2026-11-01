# Copia local temporal de `SeatsReservation.Auth`

Espejo de `src/Common/SeatsReservation.Auth` de la rama `origin/feature/06-auth-server-m2m` (commit `67a7e32`), que todavía
no está en `develop`. Todo lo que es "librería compartida" vive **solo en esta carpeta**, con el mismo namespace
(`SeatsReservation.Auth`) y la misma API, para poder eliminarla sin tocar el resto del código (TD-6).

**Para migrar:** (1) borrar esta carpeta, (2) agregar un `ProjectReference` a `SeatsReservation.Auth` en `Availability.Web`,
(3) agregar la línea `COPY src/Common/SeatsReservation.Auth/*.csproj` al Dockerfile. `Program.cs` no cambia.

Lo propio de Availability (401/403 con `ProblemDetails`, usuario actual, roles en controllers) está **fuera** de esta carpeta.
