using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct OrderId(Guid Value) : IEntityId
{
    public static OrderId New() => new(Guid.NewGuid());

    public static Result<OrderId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<OrderId>(DomainErrors.EmptyIdentifier(nameof(OrderId)))
            : Result.Success(new OrderId(value));

    public override string ToString() => Value.ToString();
}
