using Microsoft.Extensions.DependencyInjection;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Application.Services;

namespace CitizenPortal.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        return services;
    }
}
