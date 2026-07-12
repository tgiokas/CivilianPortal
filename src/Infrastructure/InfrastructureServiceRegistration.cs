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
using CitizenPortal.Infrastructure.Services;

namespace CitizenPortal.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // === Bind Settings from env variables
        var portalSettings = PortalSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(portalSettings));

        var keycloakSettings = KeycloakSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(keycloakSettings));

        var kafkaSettings = KafkaSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(kafkaSettings));

        var storageClientSettings = StorageClientSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(storageClientSettings));

        var archiumClientSettings = ArchiumClientSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(archiumClientSettings));

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
        services.AddScoped<IAuthenticationAuditLogRepository, AuthenticationAuditLogRepository>();

        // === Application PDF generation ===
        // PdfSharpCore uses a process-wide static font resolver. The resolver
        // reads TTF files embedded inside this assembly.
        EmbeddedFontResolver.Register();
        services.AddSingleton<IApplicationPdfGenerator, PdfSharpApplicationPdfGenerator>();

        // === Antivirus (External-Portal API, section 3.9) ===
        // No real ClamAV integration yet — pass-through until one is wired in.
        services.AddSingleton<IAntivirusScanner, NoOpAntivirusScanner>();

        // === Kafka ===
        services.AddSingleton<IMessagePublisher, KafkaPublisher>();
        services.AddSingleton<IEmailSender, KafkaEmailSender>();

        // === Background Services ===
        services.AddHostedService<OutboxProcessor>();           // Publishes outbox -> Kafka
        services.AddHostedService<ProtocolAssignedConsumer>();  // Consumes DMS -> updates application status

        // === HTTP Clients ===
        var keycloakBaseUrl = keycloakSettings.BaseUrl.EndsWith('/')
            ? keycloakSettings.BaseUrl
            : keycloakSettings.BaseUrl + "/";
        services.AddHttpClient<IKeycloakApiClient, KeycloakApiClient>(client =>
        {
            client.BaseAddress = new Uri(keycloakBaseUrl);
        });

        services.AddHttpClient<IStorageApiClient, StorageApiClient>(client =>
        {
            client.BaseAddress = new Uri(storageClientSettings.BaseUrl);
        });

        services.AddHttpClient<IArchiumApiClient, ArchiumApiClient>(client =>
        {
            client.BaseAddress = new Uri(archiumClientSettings.BaseUrl);
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