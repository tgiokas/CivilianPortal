using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Serilog;

using CitizenPortal.Api.Middlewares;
using CitizenPortal.Api.Services;
using CitizenPortal.Application;
using CitizenPortal.Application.Configuration;
using CitizenPortal.Infrastructure;
using CitizenPortal.Infrastructure.Database;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Register health check services
builder.Services.AddHealthChecks();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
Log.Information("Configuration is starting...");

builder.Host.UseSerilog();

// Add Kestrel server options to allow large file uploads (up to 50 MB)
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 50L * 1024 * 1024); // 50 MB

// Add Application services
builder.Services.AddApplicationServices();

// Infrastructure (Settings, DB, Repos, Kafka, HttpClients)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Bind KeycloakSettings early so we can use it for JWT config
var keycloakSettings = KeycloakSettings.BindFromConfiguration(builder.Configuration);
var portalsettings = PortalSettings.BindFromConfiguration(builder.Configuration);

// Keycloak Role Mapper
builder.Services.AddSingleton<KeycloakRoleMapper>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums (e.g. ApplicationStatus) as their string names so the SPA can
        // index lookup tables by `"Submitted" | "Registered" | ...` instead of numeric values.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configure Authentication (CitizenRealm) & Keycloak JWT Bearer
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

// CORS: AllowAnyOrigin is incompatible with credentials (SPA fetch + cookies).
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        policyBuilder.WithOrigins(portalsettings.CorsAllowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add Swagger in Development environment only
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

app.UseForwardedHeaders();

// Expose a simple health endpoint at /health
app.MapHealthChecks("/health");

Log.Information("Application is starting...");

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
app.UseMiddleware<LogMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();