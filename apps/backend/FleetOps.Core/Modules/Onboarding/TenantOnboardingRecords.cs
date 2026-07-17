namespace FleetOps.Core.Modules.Onboarding;

public sealed class TenantInvitation
{
    private TenantInvitation() { }

    public TenantInvitation(
        Guid organizationId,
        string email,
        string fullName,
        string role,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        Guid? driverId = null)
    {
        Id = Guid.NewGuid(); OrganizationId = organizationId; Email = email; FullName = fullName; Role = role;
        TokenHash = tokenHash; CreatedAtUtc = DateTimeOffset.UtcNow; ExpiresAtUtc = expiresAtUtc;
        DriverId = driverId;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public Guid? DriverId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public long RowVersion { get; private set; }
    public bool IsUsable(DateTimeOffset now) => AcceptedAtUtc is null && ExpiresAtUtc > now;
    public void Accept(DateTimeOffset now)
    {
        AcceptedAtUtc = now;
        RowVersion++;
    }
}

public sealed class DriverPairingCode
{
    private DriverPairingCode() { }

    public DriverPairingCode(Guid organizationId, Guid userId, string codeHash, DateTimeOffset expiresAtUtc)
    {
        Id = Guid.NewGuid(); OrganizationId = organizationId; UserId = userId; CodeHash = codeHash;
        CreatedAtUtc = DateTimeOffset.UtcNow; ExpiresAtUtc = expiresAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public long RowVersion { get; private set; }
    public bool IsUsable(DateTimeOffset now) => ConsumedAtUtc is null && ExpiresAtUtc > now;
    public void Consume(DateTimeOffset now)
    {
        ConsumedAtUtc = now;
        RowVersion++;
    }
}

public sealed class OnboardingImportSession
{
    private OnboardingImportSession() { }

    public OnboardingImportSession(
        Guid organizationId,
        Guid createdByUserId,
        string targetType,
        string rowsJson,
        string errorsJson,
        int rowCount,
        int errorCount,
        DateTimeOffset expiresAtUtc)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;
        TargetType = targetType;
        RowsJson = rowsJson;
        ErrorsJson = errorsJson;
        RowCount = rowCount;
        ErrorCount = errorCount;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string TargetType { get; private set; } = string.Empty;
    public string RowsJson { get; private set; } = string.Empty;
    public string ErrorsJson { get; private set; } = string.Empty;
    public int RowCount { get; private set; }
    public int ErrorCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public string? SummaryJson { get; private set; }
    public long RowVersion { get; private set; }

    public bool CanConfirm(DateTimeOffset now) =>
        ConfirmedAtUtc is null && ErrorCount == 0 && RowCount > 0 && ExpiresAtUtc > now;

    public void Confirm(string summaryJson, DateTimeOffset now)
    {
        if (!CanConfirm(now))
        {
            throw new InvalidOperationException("Import preview cannot be confirmed.");
        }

        SummaryJson = summaryJson;
        ConfirmedAtUtc = now;
        RowVersion++;
    }
}

public sealed class OnboardingSampleDataSet
{
    private OnboardingSampleDataSet() { }

    public OnboardingSampleDataSet(
        Guid organizationId,
        Guid vehicleId,
        Guid driverId,
        Guid deviceId,
        Guid missionId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        VehicleId = vehicleId;
        DriverId = driverId;
        DeviceId = deviceId;
        MissionId = missionId;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid DriverId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid MissionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class OnboardingActivationEvent
{
    private OnboardingActivationEvent() { }

    public OnboardingActivationEvent(Guid organizationId, Guid? userId, string eventName, string step, DateTimeOffset occurredAtUtc)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        UserId = userId;
        EventName = eventName;
        Step = step;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? UserId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string Step { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
