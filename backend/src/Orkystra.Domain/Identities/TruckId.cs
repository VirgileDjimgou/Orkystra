using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct TruckId(Guid Value) : IEntityId
{
    public static TruckId New() => new(Guid.NewGuid());

    public static Result<TruckId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<TruckId>(DomainErrors.EmptyIdentifier(nameof(TruckId)))
            : Result.Success(new TruckId(value));

    public override string ToString() => Value.ToString();
}
