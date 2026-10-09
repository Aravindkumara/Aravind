using Microsoft.Extensions.DependencyInjection;
using ServiceExcellence.Core.Interfaces;
using ServiceExcellence.Data.Repositories;

namespace ServiceExcellence.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDealerRepository, DealerRepository>();
        services.AddScoped<IMasterRepository, MasterRepository>();
        services.AddScoped<IAssemblyRepository, AssemblyRepository>();
        services.AddScoped<ISopRepository, SopRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        return services;
    }
}
