using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct ShipmentId(Guid Value) : IEntityId
{
    public static ShipmentId New() => new(Guid.NewGuid());

    public static Result<ShipmentId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<ShipmentId>(DomainErrors.EmptyIdentifier(nameof(ShipmentId)))
            : Result.Success(new ShipmentId(value));

    public override string ToString() => Value.ToString();
}
