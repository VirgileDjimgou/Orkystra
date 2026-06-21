namespace Orkystra.Domain.Common;

public static class DomainErrors
{
    public static DomainError EmptyIdentifier(string identifierName) =>
        new("domain.identifier.empty", $"{identifierName} cannot be empty.");

    public static DomainError InvalidValue(string valueName, string reason) =>
        new("domain.value.invalid", $"{valueName} is invalid: {reason}");

    public static DomainError Required(string valueName) =>
        new("domain.value.required", $"{valueName} is required.");
}
