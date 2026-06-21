using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct RouteId(Guid Value) : IEntityId
{
    public static RouteId New() => new(Guid.NewGuid());

    public static Result<RouteId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<RouteId>(DomainErrors.EmptyIdentifier(nameof(RouteId)))
            : Result.Success(new RouteId(value));

    public override string ToString() => Value.ToString();
}
