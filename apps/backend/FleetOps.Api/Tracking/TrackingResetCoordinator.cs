using System.Collections.Concurrent;

namespace FleetOps.Api.Tracking;

/// <summary>
/// Serializes synthetic-scenario resets per organization so concurrent resets and
/// ingestion races stay bounded and predictable within a single API instance.
/// </summary>
public sealed class TrackingResetCoordinator
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(organizationId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
