FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PedeLocal.slnx ./
COPY src/PedeLocal.Domain/PedeLocal.Domain.csproj src/PedeLocal.Domain/
COPY src/PedeLocal.Application/PedeLocal.Application.csproj src/PedeLocal.Application/
COPY src/PedeLocal.Infrastructure/PedeLocal.Infrastructure.csproj src/PedeLocal.Infrastructure/
COPY src/PedeLocal.Contracts/PedeLocal.Contracts.csproj src/PedeLocal.Contracts/
COPY src/PedeLocal.Api/PedeLocal.Api.csproj src/PedeLocal.Api/
RUN dotnet restore src/PedeLocal.Api/PedeLocal.Api.csproj

COPY src/ src/
RUN dotnet publish src/PedeLocal.Api/PedeLocal.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql's SASL/GSS negotiation dlopens libgssapi_krb5.so.2, which the base image doesn't ship —
# without it every connection attempt logs "Cannot load library libgssapi_krb5.so.2" and falls
# back noisily instead of negotiating cleanly.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --uid 5678 --create-home appuser

# COPY (and the mkdir/chown below) run as root: WORKDIR created /app as root before appuser
# existed, so copying while already USER appuser would leave /app itself root-owned — appuser
# could read the published files but not create new entries like the uploads directory at
# runtime (Program.cs calls Directory.CreateDirectory for local file storage). Chown once here,
# then drop to appuser for everything that actually runs the app.
COPY --from=build /app .
RUN mkdir -p /app/uploads && chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PedeLocal.Api.dll"]
