# Availability (Clean Architecture mínima).
# Availability.Application referencia src/Common/SeatsReservation.Contracts y Availability.Web la librería compartida
# src/Common/SeatsReservation.Auth y SeatsReservation.Observability (raíz), por eso el
# contexto de build debe ser la RAÍZ del repositorio:
#   docker build -f src/Services/Availability/Dockerfile -t availability .
#   docker run --rm -p 5101:5101 availability
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

# Restore con solo los .csproj para cachear capas
COPY src/Common/SeatsReservation.Observability/*.csproj src/Common/SeatsReservation.Observability/
COPY src/Common/SeatsReservation.Contracts/*.csproj src/Common/SeatsReservation.Contracts/
COPY src/Common/SeatsReservation.Auth/*.csproj src/Common/SeatsReservation.Auth/
COPY src/Services/Availability/src/Availability.Domain/*.csproj src/Services/Availability/src/Availability.Domain/
COPY src/Services/Availability/src/Availability.Application/*.csproj src/Services/Availability/src/Availability.Application/
COPY src/Services/Availability/src/Availability.Infrastructure/*.csproj src/Services/Availability/src/Availability.Infrastructure/
COPY src/Services/Availability/src/Availability.Web/*.csproj src/Services/Availability/src/Availability.Web/
RUN dotnet restore src/Services/Availability/src/Availability.Web/Availability.Web.csproj

COPY src/Common/SeatsReservation.Observability/ src/Common/SeatsReservation.Observability/
COPY src/Common/SeatsReservation.Contracts/ src/Common/SeatsReservation.Contracts/
COPY src/Common/SeatsReservation.Auth/ src/Common/SeatsReservation.Auth/
COPY src/Services/Availability/src/ src/Services/Availability/src/
RUN dotnet publish src/Services/Availability/src/Availability.Web/Availability.Web.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=5101
EXPOSE 5101
ENTRYPOINT ["dotnet", "Availability.Web.dll"]
