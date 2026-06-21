namespace Orkystra.Domain.Simulation;

public sealed record InjectedSimulationEvent(string EventType, string AggregateReference, string ReasonCode, int Severity);
