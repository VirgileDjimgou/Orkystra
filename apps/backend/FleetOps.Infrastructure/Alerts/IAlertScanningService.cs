namespace FleetOps.Infrastructure.Alerts;

public interface IAlertScanningService
{
    Task<AlertScanResult> ScanAllOrganizationsAsync(CancellationToken cancellationToken);
    Task<AlertScanResult> ScanOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
}
