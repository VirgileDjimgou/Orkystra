using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct WarehouseId(Guid Value) : IEntityId
{
    public static WarehouseId New() => new(Guid.NewGuid());

    public static Result<WarehouseId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<WarehouseId>(DomainErrors.EmptyIdentifier(nameof(WarehouseId)))
            : Result.Success(new WarehouseId(value));

    public override string ToString() => Value.ToString();
}
