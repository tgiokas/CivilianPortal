using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Serilog;

using CitizenPortal.Api.Middlewares;
using CitizenPortal.Api.Services;
using CitizenPortal.Application.Configuration;
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
    
    // Infrastructure (Settings, DB, Repos, Services, Kafka, HttpClients)
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // Bind KeycloakSettings early so we can use it for JWT config
    var keycloakSettings = KeycloakSettings.BindFromConfiguration(builder.Configuration);

    // Keycloak Role Mapper
    builder.Services.AddSingleton<KeycloakRoleMapper>();

    builder.Services.AddControllers();

    // Authentication — CitizenRealm (same pattern as Auth's Program.cs)
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = keycloakSettings.Authority;
            options.Audience = keycloakSettings.ClientId;
            options.RequireHttpsMetadata = keycloakSettings.RequireHttpsMetadata;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = keycloakSettings.Authority,
                ValidateAudience = true,
                ValidAudiences = [keycloakSettings.ClientId],
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

    //builder.Services.AddAuthorization(options =>
    //{
    //    options.AddPolicy("CitizenOnly", policy => policy.RequireRole("citizen"));
    //});

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

    // Health checks
    builder.Services.AddHealthChecks();   

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    var app = builder.Build();

    // Expose a simple health endpoint at /health
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
