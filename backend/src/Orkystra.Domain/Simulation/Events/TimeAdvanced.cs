using Orkystra.Domain.Events;
using Orkystra.Domain.Identities;

namespace Orkystra.Domain.Simulation.Events;

public sealed record TimeAdvanced(ScenarioId ScenarioId, DateTimeOffset CurrentTime, long StepTicks) : DomainEvent;
