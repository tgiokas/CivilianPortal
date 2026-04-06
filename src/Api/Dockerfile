FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Domain/CitizenPortal.Domain.csproj", "src/Domain/"]
COPY ["src/Application/CitizenPortal.Application.csproj", "src/Application/"]
COPY ["src/Infrastructure/CitizenPortal.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Api/CitizenPortal.Api.csproj", "src/Api/"]
RUN dotnet restore "src/Api/CitizenPortal.Api.csproj"

COPY . .
WORKDIR "/src/src/Api"
RUN dotnet build "CitizenPortal.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CitizenPortal.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY error-codes/errors.json .
ENTRYPOINT ["dotnet", "CitizenPortal.Api.dll"]
