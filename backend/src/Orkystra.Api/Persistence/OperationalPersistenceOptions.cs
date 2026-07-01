namespace Orkystra.Api.Persistence;

public sealed class OperationalPersistenceOptions
{
    public const string SectionName = "OperationalPersistence";

    public string Provider { get; init; } = "sqlite";

    public string DatabasePath { get; init; } = Path.Combine("output", "persistence", "orkystra-operations.db");

    public string ConnectionString { get; init; } = "Host=localhost;Port=5432;Database=orkystra;Username=orkystra;Password=orkystra";

    public int ReadLimit { get; init; } = 200;
}
