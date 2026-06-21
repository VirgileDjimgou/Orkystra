using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct ZoneId(Guid Value) : IEntityId
{
    public static ZoneId New() => new(Guid.NewGuid());

    public static Result<ZoneId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<ZoneId>(DomainErrors.EmptyIdentifier(nameof(ZoneId)))
            : Result.Success(new ZoneId(value));

    public override string ToString() => Value.ToString();
}
