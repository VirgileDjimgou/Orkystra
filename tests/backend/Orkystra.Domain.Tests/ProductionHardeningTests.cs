using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orkystra.Api.Observability;
using Orkystra.Api.Security;
using Orkystra.Api.Tenancy;

namespace Orkystra.Domain.Tests;

public sealed class ProductionHardeningTests
{
    [Fact]
    public void ApiKeyValidator_accepts_matching_key_and_rejects_mismatch()
    {
        var validator = new ApiKeyValidator(
            Options.Create(new ApiSecurityOptions
            {
                ApiKey = "expected-key",
                ApiKeyHeaderName = "X-Api-Key"
            }));

        Assert.True(validator.IsValid("expected-key"));
        Assert.False(validator.IsValid("wrong-key"));
    }

    [Fact]
    public void TenantResolutionService_uses_default_tenant_in_local_mode()
    {
        var resolver = new TenantResolutionService(
            Options.Create(new TenantAccessOptions
            {
                RequireTenantHeader = false,
                DefaultTenantId = "north-hub-demo",
                TenantHeaderName = "X-Tenant-Id"
            }));

        var resolution = resolver.Resolve(new HeaderDictionary());

        Assert.True(resolution.Success);
        Assert.Equal("north-hub-demo", resolution.TenantId);
    }

    [Fact]
    public void TenantResolutionService_requires_header_when_enforced()
    {
        var resolver = new TenantResolutionService(
            Options.Create(new TenantAccessOptions
            {
                RequireTenantHeader = true,
                DefaultTenantId = "ignored",
                TenantHeaderName = "X-Tenant-Id"
            }));

        var resolution = resolver.Resolve(new HeaderDictionary());

        Assert.False(resolution.Success);
        Assert.Contains("X-Tenant-Id", resolution.FailureReason);
    }

    [Fact]
    public void RequestMetricsStore_tracks_success_and_failure_counts()
    {
        var store = new RequestMetricsStore();

        store.Record(StatusCodes.Status200OK);
        store.Record(StatusCodes.Status401Unauthorized);
        store.Record(StatusCodes.Status503ServiceUnavailable);

        var snapshot = store.Snapshot();

        Assert.Equal(3, snapshot.TotalRequests);
        Assert.Equal(1, snapshot.SuccessfulRequests);
        Assert.Equal(2, snapshot.FailedRequests);
    }

    [Fact]
    public void ObservabilityOptions_expose_audit_persistence_defaults()
    {
        var options = new ObservabilityOptions();

        Assert.True(options.EnableAuditLogging);
        Assert.Contains("audit-log.jsonl", options.AuditLogFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(200, options.AuditReadLimit);
    }
}
