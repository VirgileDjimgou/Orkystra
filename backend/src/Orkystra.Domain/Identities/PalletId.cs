using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct PalletId(Guid Value) : IEntityId
{
    public static PalletId New() => new(Guid.NewGuid());

    public static Result<PalletId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<PalletId>(DomainErrors.EmptyIdentifier(nameof(PalletId)))
            : Result.Success(new PalletId(value));

    public override string ToString() => Value.ToString();
}
