namespace Orkystra.Api.Observability;

public sealed class RequestMetricsStore
{
    private int _totalRequests;
    private int _successfulRequests;
    private int _failedRequests;

    public void Record(int statusCode)
    {
        Interlocked.Increment(ref _totalRequests);
        if (statusCode >= 200 && statusCode < 400)
        {
            Interlocked.Increment(ref _successfulRequests);
        }
        else
        {
            Interlocked.Increment(ref _failedRequests);
        }
    }

    public RequestMetricsSnapshot Snapshot() =>
        new(_totalRequests, _successfulRequests, _failedRequests);
}
