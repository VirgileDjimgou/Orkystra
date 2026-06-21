namespace Orkystra.Api.Observability;

public sealed record RequestMetricsSnapshot(int TotalRequests, int SuccessfulRequests, int FailedRequests);
