using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct TenantId(Guid Value) : IEntityId
{
    public static TenantId New() => new(Guid.NewGuid());

    public static Result<TenantId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<TenantId>(DomainErrors.EmptyIdentifier(nameof(TenantId)))
            : Result.Success(new TenantId(value));

    public override string ToString() => Value.ToString();
}
