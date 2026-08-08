using OneC.Application;
using OneC.Infrastructure;

namespace OneC.Api;

/// <summary>
///     Provides dependency injection registrations for the API layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Registers all dependencies required by the API layer.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Updated service collection.</returns>
    public static IServiceCollection AddApiDi(this IServiceCollection services)
    {
        services.AddApplicationDi().AddInfrastructureDi();

        return services;
    }
}