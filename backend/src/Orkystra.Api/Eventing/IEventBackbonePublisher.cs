using Orkystra.Contracts.Eventing;

namespace Orkystra.Api.Eventing;

public interface IEventBackbonePublisher
{
    ValueTask PublishAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default);
}
