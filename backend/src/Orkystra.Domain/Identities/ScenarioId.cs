using Orkystra.Domain.Abstractions;
using Orkystra.Domain.Common;

namespace Orkystra.Domain.Identities;

public readonly record struct ScenarioId(Guid Value) : IEntityId
{
    public static ScenarioId New() => new(Guid.NewGuid());

    public static Result<ScenarioId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<ScenarioId>(DomainErrors.EmptyIdentifier(nameof(ScenarioId)))
            : Result.Success(new ScenarioId(value));

    public override string ToString() => Value.ToString();
}
