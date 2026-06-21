namespace Orkystra.Api.Tenancy;

public sealed record TenantResolutionResult(bool Success, string? TenantId, string? FailureReason);
