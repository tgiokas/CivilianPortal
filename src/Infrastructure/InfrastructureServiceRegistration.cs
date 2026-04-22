using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Interfaces;
using CitizenPortal.Infrastructure.Database;
using CitizenPortal.Infrastructure.Messaging;
using CitizenPortal.Infrastructure.Repositories;
using CitizenPortal.Infrastructure.ExternalServices;

namespace CitizenPortal.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // === Bind Settings from env variables (same pattern as Auth) ===
        var portalSettings = PortalSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(portalSettings));

        var keycloakSettings = KeycloakSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(keycloakSettings));

        var kafkaSettings = KafkaSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(kafkaSettings));

        var storageClientSettings = StorageClientSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(storageClientSettings));

        // === Database ===
        var connectionString = portalSettings.DbConnection;
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

        // === Kafka ===
        services.AddSingleton<IMessagePublisher, KafkaPublisher>();

        // === Background Services ===
        services.AddHostedService<OutboxProcessor>();           // Publishes outbox → Kafka
        services.AddHostedService<ProtocolAssignedConsumer>();  // Consumes DMS → updates status

        // === HTTP Clients ===
        // Both now consistent — BaseAddress set at DI level
        services.AddHttpClient<IKeycloakApiClient, KeycloakApiClient>(client =>
        {
            client.BaseAddress = new Uri(keycloakSettings.BaseUrl);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddHttpClient<IStorageApiClient, StorageApiClient>(client =>
        {
            client.BaseAddress = new Uri(storageClientSettings.BaseUrl);
        });

        // Add Error Catalog Path
        var path = Path.Combine(Environment.CurrentDirectory, "errors.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"errors.json not found at: {path}");

        var errorcat = ErrorCatalog.LoadFromFile(path);
        services.AddSingleton<IErrorCatalog>(errorcat);

        return services;
    }
}
