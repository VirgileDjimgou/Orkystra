namespace FleetOps.Core.Modules.Identity;

public sealed class Organization
{
    private Organization() { }

    public Organization(string name, string slug)
    {
        Name = Require(name, nameof(name), 160);
        Slug = Require(slug, nameof(slug), 80).ToLowerInvariant();
    }

    public Guid Id { get; private init; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private init; } = DateTimeOffset.UtcNow;

    private static string Require(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
