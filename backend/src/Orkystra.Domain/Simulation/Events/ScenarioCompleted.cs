using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Simulation.Events;

public sealed record ScenarioCompleted(ScenarioId ScenarioId, DateTimeOffset CompletedAt) : DomainEvent;
