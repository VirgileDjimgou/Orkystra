using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Infrastructure.Persistence;

public static class FleetOpsSeedData
{
    public static async Task EnsureSeededAsync(
        FleetOpsDbContext dbContext,
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        foreach (var role in SystemRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        if (!await dbContext.Organizations.AnyAsync(cancellationToken))
        {
            var north = new Organization("Northwind Logistics", "northwind");
            var south = new Organization("Southridge Transport", "southridge");
            dbContext.Organizations.AddRange(north, south);

            var northVan = new Vehicle(north.Id, "NW-100", "Northwind Dispatch Van");
            var southHauler = new Vehicle(south.Id, "SR-200", "Southridge Line Hauler");
            var backupVan = new Vehicle(north.Id, "NW-101", "Northwind Reserve Van");
            dbContext.Vehicles.AddRange(northVan, southHauler, backupVan);

            var northDriver = new Driver(north.Id, "Alex North", "NW-DL-001", "+1-555-0100");
            var southDriver = new Driver(south.Id, "Sam South", "SR-DL-002", "+1-555-0200");
            var inactiveDriver = new Driver(north.Id, "Inactive North", "NW-DL-002");
            inactiveDriver.Deactivate();
            dbContext.Drivers.AddRange(northDriver, southDriver, inactiveDriver);

            var northDevice = new GpsDevice(north.Id, "NW-GPS-100", "Van tracker");
            var southDevice = new GpsDevice(south.Id, "SR-GPS-200", "Line hauler tracker");
            var northSpareDevice = new GpsDevice(north.Id, "NW-GPS-101", "Spare device");
            dbContext.GpsDevices.AddRange(northDevice, southDevice, northSpareDevice);

            var historicalUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
            dbContext.DeviceAssignments.AddRange(
                new DeviceAssignment(north.Id, northDevice.Id, northVan.Id, historicalUtc),
                new DeviceAssignment(south.Id, southDevice.Id, southHauler.Id, historicalUtc));

            await dbContext.SaveChangesAsync(cancellationToken);

            await EnsureUserAsync(userManager, north, "admin@northwind.local", "Northwind Admin", "Admin123!", SystemRoles.Admin);
            await EnsureUserAsync(userManager, north, "operator@northwind.local", "Northwind Operator", "Operator123!", SystemRoles.Operator);
            await EnsureUserAsync(userManager, north, "driver@northwind.local", "Northwind Driver", "Driver123!", SystemRoles.Driver);
            await EnsureUserAsync(userManager, south, "admin@southridge.local", "Southridge Admin", "Admin123!", SystemRoles.Admin);
            await EnsureUserAsync(userManager, south, "operator@southridge.local", "Southridge Operator", "Operator123!", SystemRoles.Operator);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        Organization organization,
        string email,
        string fullName,
        string password,
        string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            OrganizationId = organization.Id,
            EmailConfirmed = true,
            IsActive = true,
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Could not seed user {email}: {string.Join("; ", createResult.Errors.Select(x => x.Description))}");
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException($"Could not add role {role} to {email}: {string.Join("; ", roleResult.Errors.Select(x => x.Description))}");
        }
    }
}
