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
using Microsoft.OpenApi.Models;
using CitizenPortal.Application.Constants;

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

// Kestrel ceiling sits above the app-level limit enforced by FileSizeLimitFilter,
// so the filter's Result<T> error always trips first; this is just the backstop.
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = SizeLimitsConstants.KestrelMaxRequestBodySize);

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

// Configure Authentication (CitizenRealm) & Keycloak JWT Bearer.
// Authority = public issuer stamped on tokens (must match JWT "iss").
// MetadataAddress = in-cluster discovery/JWKS (KEYCLOAK_BASEURL) so containers
// don't fetch OIDC config via the browser-facing hostname.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakSettings.Authority;
        options.MetadataAddress =
            $"{keycloakSettings.BaseUrl.TrimEnd('/')}/realms/{keycloakSettings.CitizenRealm}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = keycloakSettings.RequireHttpsMetadata;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakSettings.Authority,
            ValidateAudience = true,
            ValidAudiences = keycloakSettings.ValidAudiences,
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

// OPS ExternalPortal: authenticated + azp must be the OPS service-account client
// (KEYCLOAK_OPS_CLIENTID). Citizen-app tokens validate as JWT but fail this policy.
//
// CitizenPortal: authenticated + azp must be the citizen SPA client (KEYCLOAK_PORTAL_CLIENTID).
// Citizens obtain their token via the browser authorization-code flow through oauth2-proxy,
// not client-credentials, so OPS service tokens validate as JWT but fail this policy.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OpsExternalPortal", policy =>
    {
        policy.RequireAuthenticatedUser();
        if (!string.IsNullOrWhiteSpace(keycloakSettings.OpsClientId))
        {
            policy.RequireClaim("azp", keycloakSettings.OpsClientId);
        }
    });

    options.AddPolicy("CitizenPortal", policy =>
    {
        policy.RequireAuthenticatedUser();
        if (!string.IsNullOrWhiteSpace(keycloakSettings.ClientId))
        {
            policy.RequireClaim("azp", keycloakSettings.ClientId);
        }
    });
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

// Public path Traefik strips before the request reaches Kestrel (e.g. /api/citizen-portal).
// Without this, Swagger "Try it out" calls host-root paths and hits the SPA nginx (405).
// Override with PUBLIC_API_PATH_PREFIX (empty disables); otherwise inferred from KEYCLOAK_PORTAL_REDIRECTURI.
var publicApiPathPrefix = ResolvePublicApiPathPrefix(builder.Configuration);

// Add Swagger in Development and Staging environments
var shouldEnableSwagger = builder.Environment.IsDevelopment() || builder.Environment.IsStaging();
if (shouldEnableSwagger)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            // Swagger UI adds the "Bearer " prefix itself — paste the raw JWT only.
            Description = "Paste the raw JWT access_token only (do not type the word Bearer)."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
}

var app = builder.Build();

app.UseForwardedHeaders();

// Force PathBase so redirects / OpenAPI links keep the public prefix after Traefik stripPrefix.
if (!string.IsNullOrEmpty(publicApiPathPrefix))
{
    app.Use((context, next) =>
    {
        context.Request.PathBase = publicApiPathPrefix;
        return next();
    });
}

// Expose a simple health endpoint at /health
app.MapHealthChecks("/health");

Log.Information("Application is starting...");

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger(options =>
    {
        if (string.IsNullOrEmpty(publicApiPathPrefix))
            return;

        options.PreSerializeFilters.Add((document, _) =>
        {
            document.Servers =
            [
                new OpenApiServer { Url = publicApiPathPrefix }
            ];
        });
    });
    app.UseSwaggerUI(options =>
    {
        if (!string.IsNullOrEmpty(publicApiPathPrefix))
        {
            options.SwaggerEndpoint($"{publicApiPathPrefix}/swagger/v1/swagger.json", "Citizen Portal API v1");
        }
    });
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

static string ResolvePublicApiPathPrefix(IConfiguration configuration)
{
    // Explicit override: empty string disables prefix (direct Kestrel access without Traefik).
    var explicitPrefix = configuration["PUBLIC_API_PATH_PREFIX"];
    if (explicitPrefix is not null)
        return explicitPrefix.TrimEnd('/');

    // Infer from OAuth callback, e.g. https://host/api/citizen-portal/authentication/oauth2callback
    var redirect = configuration["KEYCLOAK_PORTAL_REDIRECTURI"];
    if (Uri.TryCreate(redirect, UriKind.Absolute, out var uri))
    {
        var path = uri.AbsolutePath.TrimEnd('/');
        const string marker = "/authentication/";
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx > 0)
            return path[..idx];
    }

    return "/api/citizen-portal";
}