namespace FleetOps.Api.Admin;

public sealed record MfaStatusResponse(
    bool IsEnabled,
    bool HasSharedKey,
    string AccountEmail);

public sealed record MfaSetupResponse(
    bool IsEnabled,
    string SharedKey,
    string ManualEntryKey,
    string AuthenticatorUri);

public sealed record VerifyMfaRequest(string Code);

public sealed record VerifyMfaResponse(
    bool IsEnabled,
    string[] RecoveryCodes);

public sealed record DisableMfaRequest(string Code);
