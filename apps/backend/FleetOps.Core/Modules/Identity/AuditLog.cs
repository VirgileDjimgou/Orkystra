namespace FleetOps.Core.Modules.Identity;

public sealed class AuditLog
{
    private AuditLog() { }

    public AuditLog(
        Guid organizationId,
        Guid? actorUserId,
        string actionType,
        string targetType,
        string? targetId,
        string? metadata)
    {
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        ActionType = Require(actionType, nameof(actionType), 64);
        TargetType = Require(targetType, nameof(targetType), 64);
        TargetId = Normalize(targetId, 128);
        Metadata = Normalize(metadata, 2048);
    }

    public Guid Id { get; private init; } = Guid.NewGuid();
    public Guid OrganizationId { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string ActionType { get; private init; } = string.Empty;
    public string TargetType { get; private init; } = string.Empty;
    public string? TargetId { get; private init; }
    public string? Metadata { get; private init; }
    public DateTimeOffset OccurredAtUtc { get; private init; } = DateTimeOffset.UtcNow;

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return Normalize(value, maxLength)!;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Value cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
