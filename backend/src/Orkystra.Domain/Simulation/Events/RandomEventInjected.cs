using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Simulation.Events;

public sealed record RandomEventInjected(
    ScenarioId ScenarioId,
    string InjectedEventType,
    string AggregateReference,
    string ReasonCode,
    int Severity) : DomainEvent;
