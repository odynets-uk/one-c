using Microsoft.Extensions.DependencyInjection;
using OneC.Application;
using OneC.Infrastructure;

namespace OneC.Cli;

/// <summary>
///     Provides dependency injection registrations for the CLI layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Registers CLI dependencies.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Updated service collection.</returns>
    public static IServiceCollection AddCliDi(this IServiceCollection services)
    {
        services.AddApplicationDi().AddInfrastructureDi();

        services.AddTransient<CliApp>();

        return services;
    }
}