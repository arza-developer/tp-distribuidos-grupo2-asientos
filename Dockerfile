# Etapa 1: Compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar todo el contexto del repositorio
COPY . .

# Argumento dinámico inyectado por el docker-compose
ARG SERVICE_NAME

# IMPORTANTE: Usar -o /out/${SERVICE_NAME} para que cada microservicio compile en su propia carpeta aislada
RUN dotnet publish "src/Services/${SERVICE_NAME}/${SERVICE_NAME}.csproj" -c Release -o /out/${SERVICE_NAME}

# Etapa 2: Imagen final optimizada para ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ARG SERVICE_NAME

# Copiar únicamente los binarios de la carpeta específica del servicio
COPY --from=build /out/${SERVICE_NAME} .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT dotnet ${SERVICE_NAME}.dll