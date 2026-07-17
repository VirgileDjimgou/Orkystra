namespace FleetOps.Api.Auth;

public sealed record LoginRequest(string Email, string Password, string? TwoFactorCode = null);

public sealed record AuthenticatedUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    string OrganizationName,
    Guid? DriverId,
    string[] Roles,
    bool TwoFactorEnabled);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUserResponse User,
    bool RequiresTwoFactor,
    string? TwoFactorProvider,
    string? ChallengeMessage);

public sealed record WebLoginResponse(
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUserResponse User,
    string CsrfToken,
    bool RequiresTwoFactor,
    string? TwoFactorProvider,
    string? ChallengeMessage);

public sealed record CsrfTokenResponse(string CsrfToken);

public sealed record UserSessionResponse(
    Guid SessionId,
    string ClientType,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsCurrent);
