using Microsoft.Extensions.DependencyInjection;
using OneC.Application.Abstractions;
using OneC.Application.Abstractions.Services;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;
using OneC.Infrastructure.Readers;
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
    
        // Register readers and related services
        services.AddScoped<ReferenceResolver>();
        services.AddScoped<RefCacheBuilder>();
        services.AddScoped<RefArrayFactory>();
        services.AddScoped<PriceTypeLoader>();
        services.AddScoped<PriceLoader>();
        services.AddScoped<StockLoader>();
        services.AddScoped<LastMovementLoader>();
        services.AddScoped<PriceCalculator>();
        services.AddScoped<StockBuilder>();
        services.AddScoped<IRegisterDataReader, RegisterDataReader>();
        services.AddScoped<CatalogReader>();

        return services;
    }
}
