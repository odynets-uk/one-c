using Microsoft.Extensions.DependencyInjection;

namespace OneC.Domain;

public static class DependencyInjection
{
    public static IServiceCollection AddDomainDi(this IServiceCollection services)
    {
        return services;
    }
}