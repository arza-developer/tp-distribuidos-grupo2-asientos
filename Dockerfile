# Etapa 1: Compilación (usamos imagen estándar de Debian para compatibilidad nativa con protoc/gRPC)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar archivos de proyectos para restaurar dependencias
COPY SeatsReservation.slnx ./
COPY src/Common/SeatsReservation.Contracts/*.csproj ./src/Common/SeatsReservation.Contracts/
COPY src/Common/SeatsReservation.Auth/*.csproj ./src/Common/SeatsReservation.Auth/
# Availability (solución Clean Architecture): el restore por ruta necesita los 4 csproj referenciados en cadena
COPY src/Services/Availability/Availability/src/Availability.Domain/*.csproj ./src/Services/Availability/Availability/src/Availability.Domain/
COPY src/Services/Availability/Availability/src/Availability.Application/*.csproj ./src/Services/Availability/Availability/src/Availability.Application/
COPY src/Services/Availability/Availability/src/Availability.Infrastructure/*.csproj ./src/Services/Availability/Availability/src/Availability.Infrastructure/
COPY src/Services/Availability/Availability/src/Availability.Web/*.csproj ./src/Services/Availability/Availability/src/Availability.Web/
COPY src/Services/Booking.Api/*.csproj ./src/Services/Booking.Api/
COPY src/Services/Payment.Grpc/*.csproj ./src/Services/Payment.Grpc/
COPY src/Services/AuthServer.Api/*.csproj ./src/Services/AuthServer.Api/
COPY src/Services/Notifications.Worker/*.csproj ./src/Services/Notifications.Worker/
COPY src/Services/ExternalIdentityProvider.Api/*.csproj ./src/Services/ExternalIdentityProvider.Api/

# SERVICE_NAME = nombre del proyecto (y de la DLL). SERVICE_PATH = carpeta del proyecto; por defecto
# src/Services/<SERVICE_NAME>, salvo servicios con estructura propia (Availability la sobrescribe en el compose).
ARG SERVICE_NAME
ARG SERVICE_PATH=src/Services/${SERVICE_NAME}

# Restaurar paquetes del servicio específico
RUN dotnet restore ./${SERVICE_PATH}/${SERVICE_NAME}.csproj

# Copiar el código fuente completo
COPY . .

# Publicar el binario en la carpeta /out
RUN dotnet publish ./${SERVICE_PATH}/${SERVICE_NAME}.csproj -c Release -o /out

# Etapa 2: Imagen final para runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copiar los binarios compilados desde la etapa build
COPY --from=build /out .

# Definir dinámicamente la DLL a ejecutar según el servicio
ARG SERVICE_NAME
ENV APP_DLL=${SERVICE_NAME}.dll

ENTRYPOINT ["sh", "-c", "exec dotnet $APP_DLL"]

