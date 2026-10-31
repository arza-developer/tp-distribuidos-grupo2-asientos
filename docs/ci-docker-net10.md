# Availability en .NET 10: Dockerfile, compose y CI

La nueva Availability (`src/Services/Availability/`, Clean Architecture sobre el template Ardalis) corre en **.NET 10**. El resto de los servicios y `SeatsReservation.Contracts` siguen en **.NET 8**. Este documento describe los archivos que permiten convivir sin conflictos.

## 1. Por qué conviven net8 y net10
- El template Ardalis apunta a net10 (`Directory.Build.props` → `net10.0`).
- Booking, Payment, AuthServer, Notifications, `Availability.Api` (stub) y Contracts son net8.
- Una imagen `aspnet:8.0` no ejecuta binarios net10, así que Availability tiene su propio Dockerfile.
- Un proyecto net10 puede referenciar a uno net8 (`Availability.UseCases` → `SeatsReservation.Contracts`). Al revés no.

## 2. Requisitos de trabajo
- **SDK 10** (`global.json` de Availability: `10.0.100`, `rollForward: latestMajor`, `allowPrerelease: true`). Rige solo dentro de `src/Services/Availability/`.
- **IDE**: Visual Studio 2022 17.14 o superior, Visual Studio 2026, o VS Code. Los `.slnx` requieren herramientas recientes.
- **Docker Desktop** con WSL2. Las imágenes son Linux, así que la versión de Windows del host no influye.

## 3. `Dockerfile.availability`
Contexto de build: **raíz del repo**, porque copia `src/Common/SeatsReservation.Contracts`.

| Etapa | Imagen | Qué hace |
|---|---|---|
| `build` | `mcr.microsoft.com/dotnet/sdk:10.0` | Copia Contracts y `src/Services/Availability/` (incluye `global.json`, `Directory.Build.props`, `Directory.Packages.props`), `dotnet restore` y `dotnet publish` de `Availability.Web` en `/out` |
| `runtime` | `mcr.microsoft.com/dotnet/aspnet:10.0` | Copia `/out`, `ASPNETCORE_HTTP_PORTS=5101`, `EXPOSE 5101`, `ENTRYPOINT dotnet Availability.Web.dll` |

El `Dockerfile` original (net8, parametrizado con `SERVICE_NAME`) **no cambia** y sigue construyendo los demás servicios, incluido `availability-api`.

## 4. docker-compose
Servicio `availability-web`:

| Campo | Valor |
|---|---|
| profile | `availability-net10` (no arranca con un `up` normal) |
| build | `context: .`, `dockerfile: Dockerfile.availability` |
| puerto | `5111:5101` (el 5101 del host sigue siendo de `availability-api`) |
| volumen | `availability-data:/data` |
| variables | `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_HTTP_PORTS=5101`, `ConnectionStrings__SqliteConnection=Data Source=/data/availability.db`, `Database__ApplyMigrationsOnStartup=true` |
| recursos | `cpus: 2.0`, `mem_limit: 512m` |

```bash
docker compose --profile availability-net10 up --build availability-web
# http://localhost:5111/scalar/v1  |  http://localhost:5111/Contributors
```

## 5. CI (`.github/workflows/ci.yml`)
Triggers: push a `main`, `develop` y `feature/**`; pull request a `main` y `develop`.

| Job | SDK | Qué hace |
|---|---|---|
| `build` | 8.0.x | restore y build de `SeatsReservation.slnx` (servicios net8, sin Availability nueva) |
| `build-availability` | 10.0.x | en `src/Services/Availability`: restore, build y test de `Availability.slnx` |
| `docker-availability` | (runner) | tras `build-availability`: `docker build -f Dockerfile.availability` |

## 6. Riesgos de convivencia y mitigación
| Riesgo | Mitigación |
|---|---|
| `Directory.Build.props`, `Directory.Packages.props` y `global.json` de Availability afectarían a todo el árbol | Viven solo en `src/Services/Availability/`; MSBuild y `dotnet` los buscan hacia arriba, así que los servicios net8 no los ven. No copiarlos a la raíz |
| SDK 8 no compila net10 | Job de CI separado con SDK 10; el job net8 no incluye la nueva solución |
| Runtime por imagen | Dockerfile propio con `sdk:10.0`/`aspnet:10.0`; los demás siguen en 8.0 |
| Contracts (net8) referenciado desde net10 | Soportado. Contracts conserva sus versiones (no hereda la gestión central de paquetes) |
| Versiones de paquetes divergentes (10.x vs 8.x) | La gestión central aplica solo bajo Availability |
| `.slnx` necesita SDK 9.0.200 o superior | El job net8 funciona porque el runner trae un SDK reciente preinstalado; Availability usa 10.0.x explícito |
| Colisión de puertos | Nueva en 5111 hasta el cutover |
| Advertencias NU1902/NU1903 (MailKit, SSH.NET, Pkcs) | No bloquean: están en `WarningsNotAsErrors` |

## 7. Cutover (pendiente, al completar el desarrollo)
1. Agregar los proyectos de Availability al `SeatsReservation.slnx`.
2. Pasar el CI a un único job con SDK 10 (los proyectos net8 compilan igual con SDK 10).
3. Compose: publicar la nueva en `5101` y quitar el profile.
4. Eliminar `Availability.Api` y el servicio `availability-api`.
