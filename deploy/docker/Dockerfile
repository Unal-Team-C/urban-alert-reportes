# Contexto de build: la raíz de este repo (urban-alert-reportes)
#   docker build -f deploy/docker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Primero solo los .csproj, para cachear el restore mientras no cambien las dependencias.
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/API/API.csproj src/API/
RUN dotnet restore src/API/API.csproj

COPY src/ src/
RUN dotnet publish src/API/API.csproj -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "API.dll"]
