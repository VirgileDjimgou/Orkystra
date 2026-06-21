using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct DriverId(Guid Value) : IEntityId
{
    public static DriverId New() => new(Guid.NewGuid());

    public static Result<DriverId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<DriverId>(DomainErrors.EmptyIdentifier(nameof(DriverId)))
            : Result.Success(new DriverId(value));

    public override string ToString() => Value.ToString();
}
