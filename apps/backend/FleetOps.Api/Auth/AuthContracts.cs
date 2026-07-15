namespace FleetOps.Api.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    string OrganizationName,
    string[] Roles);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUserResponse User);
