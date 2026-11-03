# Etapa 1: Compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar archivos de proyectos para restaurar dependencias
COPY SeatsReservation.slnx ./
COPY src/Common/SeatsReservation.Contracts/*.csproj ./src/Common/SeatsReservation.Contracts/
COPY src/Common/SeatsReservation.Auth/*.csproj ./src/Common/SeatsReservation.Auth/
# Availability (solución Clean Architecture): el restore por ruta necesita los csproj referenciados en cadena
COPY src/Services/Availability/Availability/src/Availability.Domain/*.csproj ./src/Services/Availability/Availability/src/Availability.Domain/
COPY src/Services/Availability/Availability/src/Availability.Application/*.csproj ./src/Services/Availability/Availability/src/Availability.Application/
COPY src/Services/Availability/Availability/src/Availability.Infrastructure/*.csproj ./src/Services/Availability/Availability/src/Availability.Infrastructure/
COPY src/Services/Availability/Availability/src/Availability.Web/*.csproj ./src/Services/Availability/Availability/src/Availability.Web/
COPY src/Services/Booking.Api/*.csproj ./src/Services/Booking.Api/
COPY src/Services/Payment.Grpc/*.csproj ./src/Services/Payment.Grpc/
COPY src/Services/AuthServer.Api/*.csproj ./src/Services/AuthServer.Api/
COPY src/Services/Notifications.Worker/*.csproj ./src/Services/Notifications.Worker/
COPY src/Services/ExternalIdentityProvider.Api/*.csproj ./src/Services/ExternalIdentityProvider.Api/

# Argumentos dinámicos inyectados por el docker-compose
ARG SERVICE_NAME
ARG SERVICE_PATH=src/Services/${SERVICE_NAME}

# Copiar todo el contexto del repositorio
COPY . .

# Restaurar paquetes del servicio específico utilizando la ruta dinámica
RUN dotnet restore ./${SERVICE_PATH}/${SERVICE_NAME}.csproj

# Limpiar y publicar el binario asegurando una carpeta de salida aislada por servicio
RUN rm -rf /out/${SERVICE_NAME} && \
    dotnet publish ./${SERVICE_PATH}/${SERVICE_NAME}.csproj -c Release -o /out/${SERVICE_NAME}

# Etapa 2: Imagen final optimizada para ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ARG SERVICE_NAME

# Copiar únicamente los binarios de la carpeta específica del servicio
COPY --from=build /out/${SERVICE_NAME} .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT dotnet ${SERVICE_NAME}.dll