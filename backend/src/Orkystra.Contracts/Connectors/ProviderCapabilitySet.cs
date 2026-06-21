namespace Orkystra.Contracts.Connectors;

public sealed record ProviderCapabilitySet(
    bool CanRead,
    bool CanWrite,
    bool CanStreamEvents,
    bool CanIngestCommands,
    bool CanQueryHistory,
    bool SupportsReadOnlyMode,
    bool CanReplayData);
