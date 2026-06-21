using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct RackId(Guid Value) : IEntityId
{
    public static RackId New() => new(Guid.NewGuid());

    public static Result<RackId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<RackId>(DomainErrors.EmptyIdentifier(nameof(RackId)))
            : Result.Success(new RackId(value));

    public override string ToString() => Value.ToString();
}
