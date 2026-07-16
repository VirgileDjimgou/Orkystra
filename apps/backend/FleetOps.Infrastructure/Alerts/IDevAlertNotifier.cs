using FleetOps.Core.Modules.Alerts;

namespace FleetOps.Infrastructure.Alerts;

public interface IDevAlertNotifier
{
    Task SendAsync(OperationalAlert alert, CancellationToken cancellationToken);
}
