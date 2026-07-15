using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFleetOpsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FleetOps")
            ?? "Server=localhost,14333;Database=FleetOps;User Id=sa;Password=ChangeThis_LocalOnly_123!;TrustServerCertificate=True";

        services.AddDbContext<FleetOpsDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
