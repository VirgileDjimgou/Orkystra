using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Simulation.Events;

public sealed record ScenarioStarted(ScenarioId ScenarioId, string Name, int Seed) : DomainEvent;
