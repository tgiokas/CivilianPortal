using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Serilog;

using CitizenPortal.Api.Middlewares;
using CitizenPortal.Api.Services;
using CitizenPortal.Infrastructure;
using CitizenPortal.Infrastructure.Database;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Keycloak Role Mapper
    builder.Services.AddSingleton<KeycloakRoleMapper>();

    // Authentication - CitizenRealm
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["KEYCLOAK_AUTHORITY"]
                ?? builder.Configuration["Keycloak:Authority"]
                ?? "http://keycloak:8080/realms/CitizenRealm";

            options.Audience = builder.Configuration["KEYCLOAK_CLIENTID"]
                ?? builder.Configuration["Keycloak:ClientId"]
                ?? "citizen-portal-app";

            options.RequireHttpsMetadata = bool.Parse(
                builder.Configuration["Keycloak:RequireHttpsMetadata"] ?? "false");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["KEYCLOAK_AUTHORITY"]
                    ?? builder.Configuration["Keycloak:Authority"],
                ValidateAudience = true,
                ValidAudiences = [builder.Configuration["KEYCLOAK_CLIENTID"]
                    ?? builder.Configuration["Keycloak:ClientId"]
                    ?? "citizen-portal-app"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var roleMapper = context.HttpContext.RequestServices
                        .GetRequiredService<KeycloakRoleMapper>();
                    roleMapper.MapRolesToClaims(context);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("CitizenOnly", policy => policy.RequireRole("citizen"));
    });

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", policyBuilder =>
        {
            policyBuilder.AllowAnyOrigin();
            policyBuilder.AllowAnyMethod();
            policyBuilder.AllowAnyHeader();
        });
    });

    // Infrastructure (DB, Repos, Services, Kafka, HttpClients)
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // Health checks
    builder.Services.AddHealthChecks();

    builder.Services.AddControllers();

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    var app = builder.Build();

    // Health endpoint
    app.MapHealthChecks("/health");

    Log.Information("CitizenPortal is starting...");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Auto-migrate
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
    Log.Information("Database migrations applied (if any).");

    app.UseCors("CorsPolicy");
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
