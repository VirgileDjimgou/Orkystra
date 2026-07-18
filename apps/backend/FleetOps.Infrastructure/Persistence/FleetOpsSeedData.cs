using FleetOps.Core.Modules.Fleet;
using FleetOps.Core.Modules.Identity;
using FleetOps.Core.Modules.Operations;
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
        BootstrapOptions bootstrapOptions,
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

        if (await dbContext.Organizations.AnyAsync(cancellationToken))
        {
            return;
        }

        if (bootstrapOptions.SeedDemoData)
        {
            var north = new Organization("Northwind Logistics", "northwind");
            var south = new Organization("Southridge Transport", "southridge");
            var west = new Organization("Westland Field Services", "westland");
            dbContext.Organizations.AddRange(north, south, west);

            var northVan = new Vehicle(north.Id, "NW-100", "Northwind Dispatch Van");
            var southHauler = new Vehicle(south.Id, "SR-200", "Southridge Line Hauler");
            var southRelayTruck = new Vehicle(south.Id, "SR-201", "Southridge Relay Truck");
            var southReserveTruck = new Vehicle(south.Id, "SR-202", "Southridge Reserve Truck");
            var backupVan = new Vehicle(north.Id, "NW-101", "Northwind Reserve Van");
            var serviceVan = new Vehicle(north.Id, "NW-102", "Northwind Service Van");
            var westServiceTruck = new Vehicle(west.Id, "WF-300", "Westland Service Truck");
            var westSupportVan = new Vehicle(west.Id, "WF-301", "Westland Support Van");
            var westReserveVan = new Vehicle(west.Id, "WF-302", "Westland Reserve Van");
            dbContext.Vehicles.AddRange(northVan, southHauler, southRelayTruck, southReserveTruck, backupVan, serviceVan, westServiceTruck, westSupportVan, westReserveVan);

            var northDriver = new Driver(north.Id, "Alex North", "NW-DL-001", "+1-555-0100");
            var southDriver = new Driver(south.Id, "Sam South", "SR-DL-002", "+1-555-0200");
            var inactiveDriver = new Driver(north.Id, "Inactive North", "NW-DL-002");
            inactiveDriver.Deactivate();
            var westDriver = new Driver(west.Id, "Morgan West", "WF-DL-003", "+1-555-0300");
            dbContext.Drivers.AddRange(northDriver, southDriver, inactiveDriver, westDriver);

            var northDevice = new GpsDevice(north.Id, "NW-GPS-100", "Dispatch van tracker");
            var southDevice = new GpsDevice(south.Id, "SR-GPS-200", "Line hauler tracker");
            var southRelayDevice = new GpsDevice(south.Id, "SR-GPS-201", "Relay truck tracker");
            var southReserveDevice = new GpsDevice(south.Id, "SR-GPS-202", "Reserve truck tracker");
            var northSpareDevice = new GpsDevice(north.Id, "NW-GPS-101", "Reserve van tracker");
            var northServiceDevice = new GpsDevice(north.Id, "NW-GPS-102", "Service van tracker");
            var westServiceDevice = new GpsDevice(west.Id, "WF-GPS-300", "Service truck tracker");
            var westSupportDevice = new GpsDevice(west.Id, "WF-GPS-301", "Support van tracker");
            var westReserveDevice = new GpsDevice(west.Id, "WF-GPS-302", "Reserve van tracker");
            dbContext.GpsDevices.AddRange(northDevice, southDevice, southRelayDevice, southReserveDevice, northSpareDevice, northServiceDevice, westServiceDevice, westSupportDevice, westReserveDevice);

            var historicalUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
            dbContext.DeviceAssignments.AddRange(
                new DeviceAssignment(north.Id, northDevice.Id, northVan.Id, historicalUtc),
                new DeviceAssignment(north.Id, northSpareDevice.Id, backupVan.Id, historicalUtc),
                new DeviceAssignment(north.Id, northServiceDevice.Id, serviceVan.Id, historicalUtc),
                new DeviceAssignment(south.Id, southDevice.Id, southHauler.Id, historicalUtc),
                new DeviceAssignment(south.Id, southRelayDevice.Id, southRelayTruck.Id, historicalUtc),
                new DeviceAssignment(south.Id, southReserveDevice.Id, southReserveTruck.Id, historicalUtc),
                new DeviceAssignment(west.Id, westServiceDevice.Id, westServiceTruck.Id, historicalUtc),
                new DeviceAssignment(west.Id, westSupportDevice.Id, westSupportVan.Id, historicalUtc),
                new DeviceAssignment(west.Id, westReserveDevice.Id, westReserveVan.Id, historicalUtc));

            await dbContext.SaveChangesAsync(cancellationToken);

            SeedChecklistTemplates(dbContext, north, south, west);
            await dbContext.SaveChangesAsync(cancellationToken);

            await EnsureUserAsync(userManager, north, "admin@northwind.local", "Northwind Admin", "Admin123!", SystemRoles.Admin);
            await EnsureUserAsync(userManager, north, "operator@northwind.local", "Northwind Operator", "Operator123!", SystemRoles.Operator);
            await EnsureUserAsync(userManager, north, "driver@northwind.local", "Northwind Driver", "Driver123!", SystemRoles.Driver, northDriver.Id);
            await EnsureUserAsync(userManager, south, "admin@southridge.local", "Southridge Admin", "Admin123!", SystemRoles.Admin);
            await EnsureUserAsync(userManager, south, "operator@southridge.local", "Southridge Operator", "Operator123!", SystemRoles.Operator);
            await EnsureUserAsync(userManager, south, "driver@southridge.local", "Southridge Driver", "Driver123!", SystemRoles.Driver, southDriver.Id);
            await EnsureUserAsync(userManager, west, "admin@westland.local", "Westland Admin", "Admin123!", SystemRoles.Admin);
            await EnsureUserAsync(userManager, west, "operator@westland.local", "Westland Operator", "Operator123!", SystemRoles.Operator);
            await EnsureUserAsync(userManager, west, "driver@westland.local", "Westland Driver", "Driver123!", SystemRoles.Driver, westDriver.Id);
            return;
        }

        if (bootstrapOptions.HasProvisioningValues)
        {
            var organization = new Organization(
                bootstrapOptions.OrganizationName,
                bootstrapOptions.OrganizationSlug);
            dbContext.Organizations.Add(organization);
            await dbContext.SaveChangesAsync(cancellationToken);
            await EnsureUserAsync(
                userManager,
                organization,
                bootstrapOptions.AdminEmail,
                "Fleet administrator",
                bootstrapOptions.AdminPassword,
                SystemRoles.Admin);
            return;
        }

        throw new InvalidOperationException(
            "FleetOps has no organization. Configure Bootstrap provisioning values or enable Bootstrap:SeedDemoData in Development.");
    }

    private static void SeedChecklistTemplates(
        FleetOpsDbContext dbContext,
        Organization north,
        Organization south,
        Organization west)
    {
        if (dbContext.ChecklistTemplates.Any())
        {
            return;
        }

        dbContext.ChecklistTemplates.AddRange(
            BuildPreDepartureTemplate(
                north.Id,
                "vehicle-ready",
                "Vehicle readiness",
                [
                    ("brakes", "Brakes and steering"),
                    ("lights", "Lights and signals"),
                    ("cargo-secured", "Cargo and doors secured"),
                ]),
            BuildPreDepartureTemplate(
                south.Id,
                "vehicle-ready",
                "Vehicle readiness",
                [
                    ("brakes", "Brakes and steering"),
                    ("lights", "Lights and signals"),
                    ("cargo-secured", "Cargo and doors secured"),
                ]),
            BuildPreDepartureTemplate(
                west.Id,
                "vehicle-ready",
                "Vehicle readiness",
                [
                    ("brakes", "Brakes and steering"),
                    ("lights", "Lights and signals"),
                    ("equipment-secured", "Tools and equipment secured"),
                ]));
    }

    private static ChecklistTemplate BuildPreDepartureTemplate(
        Guid organizationId,
        string code,
        string name,
        IReadOnlyList<(string Code, string Label)> items)
    {
        var template = new ChecklistTemplate(organizationId, code, name);
        for (var index = 0; index < items.Count; index++)
        {
            var (itemCode, label) = items[index];
            template.AddItem(new ChecklistTemplateItem(
                organizationId,
                template.Id,
                index + 1,
                itemCode,
                label));
        }

        return template;
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        Organization organization,
        string email,
        string fullName,
        string password,
        string role,
        Guid? driverId = null)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            if (existingUser.OrganizationId != organization.Id
                || existingUser.FullName != fullName
                || existingUser.DriverId != driverId
                || !existingUser.IsActive)
            {
                existingUser.OrganizationId = organization.Id;
                existingUser.FullName = fullName;
                existingUser.DriverId = driverId;
                existingUser.IsActive = true;
                await userManager.UpdateAsync(existingUser);
            }

            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            OrganizationId = organization.Id,
            DriverId = driverId,
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
