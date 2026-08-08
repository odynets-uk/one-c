using Microsoft.Extensions.DependencyInjection;

namespace OneC.Infrastructure;

/// <summary>
///     Provides dependency injection registrations for the infrastructure layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Registers infrastructure layer services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Updated service collection.</returns>
    public static IServiceCollection AddInfrastructureDi(this IServiceCollection services)
    {
        // Register repositories, database, external clients here.
        return services;
    }
}