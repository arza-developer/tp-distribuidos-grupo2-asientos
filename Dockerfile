# Etapa 1: Compilación (usamos imagen estándar de Debian para compatibilidad nativa con protoc/gRPC)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar archivos de proyectos para restaurar dependencias
COPY SeatsReservation.slnx ./
COPY src/Common/SeatsReservation.Contracts/*.csproj ./src/Common/SeatsReservation.Contracts/
COPY src/Common/SeatsReservation.Auth/*.csproj ./src/Common/SeatsReservation.Auth/
COPY src/Services/Availability.Api/*.csproj ./src/Services/Availability.Api/
COPY src/Services/Booking.Api/*.csproj ./src/Services/Booking.Api/
COPY src/Services/Payment.Grpc/*.csproj ./src/Services/Payment.Grpc/
COPY src/Services/AuthServer.Api/*.csproj ./src/Services/AuthServer.Api/
COPY src/Services/Notifications.Worker/*.csproj ./src/Services/Notifications.Worker/

ARG SERVICE_NAME

# Restaurar paquetes del servicio específico
RUN dotnet restore ./src/Services/${SERVICE_NAME}/${SERVICE_NAME}.csproj

# Copiar el código fuente completo
COPY . .

# Publicar el binario en la carpeta /out
RUN dotnet publish ./src/Services/${SERVICE_NAME}/${SERVICE_NAME}.csproj -c Release -o /out

# Etapa 2: Imagen final para runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copiar los binarios compilados desde la etapa build
COPY --from=build /out .

# Definir dinámicamente la DLL a ejecutar según el servicio
ARG SERVICE_NAME
ENV APP_DLL=${SERVICE_NAME}.dll

ENTRYPOINT ["sh", "-c", "exec dotnet $APP_DLL"]

