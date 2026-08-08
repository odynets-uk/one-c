using Microsoft.Extensions.DependencyInjection;
using OneC.Application.Abstractions.Services;
using OneC.Application.Services;

namespace OneC.Application;

/// <summary>
///     Provides dependency injection registrations for the application layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Registers application layer services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Updated service collection.</returns>
    public static IServiceCollection AddApplicationDi(this IServiceCollection services)
    {
        services.AddScoped<ISomeService, SomeService>();

        return services;
    }
}