using FleetOps.Core.Modules.Fleet;

namespace FleetOps.Api.Fleet;

public sealed record VehicleResponse(
    Guid Id,
    string RegistrationNumber,
    string DisplayName,
    bool IsActive,
    int CurrentOdometerKm,
    long RowVersion);

public sealed record CreateVehicleRequest(string RegistrationNumber, string DisplayName);

public sealed record UpdateVehicleRequest(string DisplayName, long RowVersion);

public sealed record ImportSummary(int Created, int Updated, int Skipped, List<string> Errors);

public sealed record DriverResponse(
    Guid Id,
    string FullName,
    string LicenseNumber,
    string? PhoneNumber,
    bool IsActive,
    long RowVersion);

public sealed record CreateDriverRequest(string FullName, string LicenseNumber, string? PhoneNumber);

public sealed record UpdateDriverRequest(string FullName, string? PhoneNumber, long RowVersion);

public sealed record GpsDeviceResponse(
    Guid Id,
    string SerialNumber,
    string? DisplayName,
    bool IsActive,
    long RowVersion,
    ActiveAssignmentResponse? ActiveAssignment);

public sealed record ActiveAssignmentResponse(
    Guid AssignmentId,
    Guid VehicleId,
    string VehicleRegistrationNumber,
    DateTimeOffset AssignedAtUtc);

public sealed record CreateGpsDeviceRequest(string SerialNumber, string? DisplayName);

public sealed record UpdateGpsDeviceRequest(string? DisplayName, long RowVersion);

public sealed record AssignDeviceRequest(Guid VehicleId);

public sealed record AssignmentResponse(
    Guid Id,
    Guid DeviceId,
    Guid VehicleId,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? UnassignedAtUtc,
    bool IsActive);
