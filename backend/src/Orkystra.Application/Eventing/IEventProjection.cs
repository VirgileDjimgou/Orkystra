using Orkystra.Contracts.Eventing;

namespace Orkystra.Application.Eventing;

public interface IEventProjection
{
    string ProjectionName { get; }

    bool CanProject(IEventEnvelope envelope);

    ValueTask ProjectAsync(IEventEnvelope envelope, CancellationToken cancellationToken = default);
}
