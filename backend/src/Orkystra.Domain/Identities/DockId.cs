using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct DockId(Guid Value) : IEntityId
{
    public static DockId New() => new(Guid.NewGuid());

    public static Result<DockId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<DockId>(DomainErrors.EmptyIdentifier(nameof(DockId)))
            : Result.Success(new DockId(value));

    public override string ToString() => Value.ToString();
}
