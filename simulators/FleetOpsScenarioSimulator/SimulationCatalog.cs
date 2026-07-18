namespace FleetOps.Simulation;

public sealed record SimulationAccount(string Role, string Email, string Password);

public sealed record SimulationTenant(
    string Slug,
    string Name,
    string Sector,
    string RegistrationPrefix,
    string DriverLicense,
    IReadOnlyList<string> ChecklistCodes,
    SimulationAccount Admin,
    SimulationAccount Operator,
    SimulationAccount Driver);

public static class SimulationCatalog
{
    public static IReadOnlyList<SimulationTenant> Tenants { get; } =
    [
        new(
            "northwind",
            "Northwind Logistics",
            "Local delivery",
            "NW-",
            "NW-DL-001",
            ["brakes", "lights", "cargo-secured"],
            new("Admin", "admin@northwind.local", "Admin123!"),
            new("Operator", "operator@northwind.local", "Operator123!"),
            new("Driver", "driver@northwind.local", "Driver123!")),
        new(
            "southridge",
            "Southridge Transport",
            "Regional transport",
            "SR-",
            "SR-DL-002",
            ["brakes", "lights", "cargo-secured"],
            new("Admin", "admin@southridge.local", "Admin123!"),
            new("Operator", "operator@southridge.local", "Operator123!"),
            new("Driver", "driver@southridge.local", "Driver123!")),
        new(
            "westland",
            "Westland Field Services",
            "Field services",
            "WF-",
            "WF-DL-003",
            ["brakes", "lights", "equipment-secured"],
            new("Admin", "admin@westland.local", "Admin123!"),
            new("Operator", "operator@westland.local", "Operator123!"),
            new("Driver", "driver@westland.local", "Driver123!")),
    ];
}
