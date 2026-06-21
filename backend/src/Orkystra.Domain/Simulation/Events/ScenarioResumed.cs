using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Simulation.Events;

public sealed record ScenarioResumed(ScenarioId ScenarioId, DateTimeOffset ResumedAt) : DomainEvent;
