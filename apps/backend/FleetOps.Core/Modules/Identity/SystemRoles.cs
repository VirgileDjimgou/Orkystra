namespace FleetOps.Core.Modules.Identity;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Driver = "Driver";

    public static readonly string[] All = [Admin, Operator, Driver];
}
