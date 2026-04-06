using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Interfaces;
using CitizenPortal.Infrastructure.ApiClients;
using CitizenPortal.Infrastructure.Database;
using CitizenPortal.Infrastructure.Messaging;
using CitizenPortal.Infrastructure.Repositories;
using CitizenPortal.Application.Services;

namespace CitizenPortal.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // === Database ===
        var connectionString = configuration["PORTAL_DB_CONNECTION"];
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            }).UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // === Repositories ===
        services.AddScoped<ICitizenUserRepository, CitizenUserRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        // === Application Services ===
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ICitizenUserService, CitizenUserService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        // === Kafka ===
        services.AddSingleton<IMessagePublisher, KafkaPublisher>();

        // === Background Services ===
        services.AddHostedService<OutboxProcessor>();           // Publishes outbox → Kafka
        services.AddHostedService<ProtocolAssignedConsumer>();  // Consumes DMS → updates status

        // === HTTP Clients (via Traefik) ===
        services.AddHttpClient<IStorageApiClient, StorageApiClient>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["DMS_STORAGE_URL"] ?? "http://dms-storage:8080");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Keycloak client for token exchange (CitizenRealm)
        services.AddHttpClient<IKeycloakClientAuthentication, KeycloakClientAuthentication>();

        // === Error Catalog ===
        var path = Path.Combine(Environment.CurrentDirectory, "errors.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"errors.json not found at: {path}");

        var errorCatalog = ErrorCatalog.LoadFromFile(path);
        services.AddSingleton<IErrorCatalog>(errorCatalog);

        return services;
    }
}
