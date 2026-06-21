using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct SlotId(Guid Value) : IEntityId
{
    public static SlotId New() => new(Guid.NewGuid());

    public static Result<SlotId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<SlotId>(DomainErrors.EmptyIdentifier(nameof(SlotId)))
            : Result.Success(new SlotId(value));

    public override string ToString() => Value.ToString();
}
