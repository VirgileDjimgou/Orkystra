namespace FleetOps.Api.Admin;

public sealed record UserSummaryResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    Guid? DriverId);

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    string Role);
