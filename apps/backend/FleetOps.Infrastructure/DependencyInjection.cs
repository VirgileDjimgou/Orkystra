using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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
        var useInMemory = configuration.GetValue<bool>("Testing:UseInMemoryDatabase");
        var inMemoryDatabaseName = configuration["Testing:DatabaseName"] ?? "fleetops-tests";

        services.AddDbContext<FleetOpsDbContext>(options =>
        {
            if (useInMemory)
            {
                options.UseInMemoryDatabase(inMemoryDatabaseName);
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        });
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FleetOpsDbContext>()
            .AddSignInManager();
        return services;
    }
}
