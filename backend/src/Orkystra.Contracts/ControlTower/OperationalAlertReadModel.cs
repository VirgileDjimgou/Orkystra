namespace Orkystra.Contracts.ControlTower;

public sealed record OperationalAlertReadModel(
    string Severity,
    string Title,
    string Description);
