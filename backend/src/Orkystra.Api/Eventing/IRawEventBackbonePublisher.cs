using Orkystra.Contracts.Eventing;

namespace Orkystra.Api.Eventing;

public interface IRawEventBackbonePublisher
{
    ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default);
}
