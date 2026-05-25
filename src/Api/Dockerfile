# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build context is the monorepo root, so all source paths are prefixed with
# `citizen-backend-service/`. This lets the build also reach the sibling
# `error-codes/` directory at the repo root (see the final COPY below).
# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["citizen-backend-service/src/Api/CitizenPortal.Api.csproj", "src/Api/"]
COPY ["citizen-backend-service/src/Application/CitizenPortal.Application.csproj", "src/Application/"]
COPY ["citizen-backend-service/src/Domain/CitizenPortal.Domain.csproj", "src/Domain/"]
COPY ["citizen-backend-service/src/Infrastructure/CitizenPortal.Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "./src/Api/CitizenPortal.Api.csproj"
COPY citizen-backend-service/ .
WORKDIR /src/src/Api
RUN dotnet build "./CitizenPortal.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CitizenPortal.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

COPY /error-codes/errors.json /app/errors.json

USER root
COPY certs/keycloak.crt /usr/local/share/ca-certificates/keycloak.crt
RUN update-ca-certificates

ENTRYPOINT ["dotnet", "CitizenPortal.Api.dll"]
