using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct DeliveryId(Guid Value) : IEntityId
{
    public static DeliveryId New() => new(Guid.NewGuid());

    public static Result<DeliveryId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<DeliveryId>(DomainErrors.EmptyIdentifier(nameof(DeliveryId)))
            : Result.Success(new DeliveryId(value));

    public override string ToString() => Value.ToString();
}
