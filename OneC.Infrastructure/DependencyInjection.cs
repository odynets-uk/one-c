using Microsoft.Extensions.DependencyInjection;
using OneC.Application.Abstractions.Services;
using OneC.Infrastructure.Com;
using OneC.Infrastructure.Services;

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
        services.AddSingleton<ComRegistrationService>();
        services.AddScoped<ITestConnectionService, TestConnectionService>();
        services.AddScoped<IMetadataService, MetadataService>();
        services.AddScoped<IGetCatalogService, GetCatalogService>();

        return services;
    }
}
