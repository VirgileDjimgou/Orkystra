using FleetOps.Infrastructure.Alerts;
using FleetOps.Infrastructure.Integrations;
using Microsoft.Extensions.Options;

namespace FleetOps.Worker;

public sealed partial class Worker(
    ILogger<Worker> logger,
    IAlertScanningService alertScanningService,
    IWebhookDispatchService webhookDispatchService,
    TimeProvider timeProvider,
    IOptions<AlertingOptions> alertingOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, alertingOptions.Value.WorkerScanIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                var utcNow = timeProvider.GetUtcNow();
                Log.WorkerHeartbeat(logger, utcNow);
            }
            try
            {
                var result = await alertScanningService.ScanAllOrganizationsAsync(stoppingToken);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    Log.AlertScanCompleted(
                        logger,
                        result.CreatedAlerts,
                        result.RefreshedAlerts,
                        result.ResolvedAlerts,
                        result.EmailFailures);
                }

                var dispatch = await webhookDispatchService.DispatchPendingAsync(stoppingToken);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    Log.WebhookDispatchCompleted(
                        logger,
                        dispatch.Delivered,
                        dispatch.Retried,
                        dispatch.DeadLettered);
                }
            }
            catch (Exception ex)
            {
                Log.AlertScanFailed(logger, ex);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "FleetOps worker heartbeat at {UtcNow}")]
        public static partial void WorkerHeartbeat(ILogger logger, DateTimeOffset utcNow);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Alert scan completed. Created={CreatedAlerts}, Refreshed={RefreshedAlerts}, Resolved={ResolvedAlerts}, EmailFailures={EmailFailures}")]
        public static partial void AlertScanCompleted(
            ILogger logger,
            int createdAlerts,
            int refreshedAlerts,
            int resolvedAlerts,
            int emailFailures);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Alert scan failed.")]
        public static partial void AlertScanFailed(ILogger logger, Exception exception);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Information,
            Message = "Webhook dispatch completed. Delivered={Delivered}, Retried={Retried}, DeadLettered={DeadLettered}")]
        public static partial void WebhookDispatchCompleted(
            ILogger logger,
            int delivered,
            int retried,
            int deadLettered);
    }
}
