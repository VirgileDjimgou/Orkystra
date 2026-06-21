using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Simulation.Events;

public sealed record ScenarioPaused(ScenarioId ScenarioId, DateTimeOffset PausedAt) : DomainEvent;
