using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct StopId(Guid Value) : IEntityId
{
    public static StopId New() => new(Guid.NewGuid());

    public static Result<StopId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<StopId>(DomainErrors.EmptyIdentifier(nameof(StopId)))
            : Result.Success(new StopId(value));

    public override string ToString() => Value.ToString();
}
