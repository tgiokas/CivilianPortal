using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;
using Serilog;

using CitizenPortal.Api.Middlewares;
using CitizenPortal.Api.Services;
using CitizenPortal.Application;
using CitizenPortal.Application.Configuration;
using CitizenPortal.Infrastructure;
using CitizenPortal.Infrastructure.Database;

Env.Load();
Env.TraversePath().Load();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add Application services
builder.Services.AddApplicationServices();

// Infrastructure (Settings, DB, Repos, Kafka, HttpClients)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Bind KeycloakSettings early so we can use it for JWT config
var keycloakSettings = KeycloakSettings.BindFromConfiguration(builder.Configuration);

// Keycloak Role Mapper
builder.Services.AddSingleton<KeycloakRoleMapper>();

builder.Services.AddControllers();

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

//builder.Services.AddAuthorization(options =>
//{
//    options.AddPolicy("CitizenOnly", policy => policy.RequireRole("citizen"));
//});

// Add CORS policy
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

// Add Swagger in Development environment only
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

