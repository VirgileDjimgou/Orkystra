namespace Orkystra.Api.Persistence;

public sealed class OperationalPersistenceOptions
{
    public const string SectionName = "OperationalPersistence";

    public string DatabasePath { get; init; } = Path.Combine("output", "persistence", "orkystra-operations.db");

    public int ReadLimit { get; init; } = 200;
}
