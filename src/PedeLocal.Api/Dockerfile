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

RUN useradd --uid 5678 --create-home appuser
USER appuser

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PedeLocal.Api.dll"]
