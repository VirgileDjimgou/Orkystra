using FleetOps.Core.Modules.Alerts;
using Microsoft.Extensions.Logging;

namespace FleetOps.Infrastructure.Alerts;

public sealed partial class LoggingDevAlertNotifier(ILogger<LoggingDevAlertNotifier> logger) : IDevAlertNotifier
{
    public Task SendAsync(OperationalAlert alert, CancellationToken cancellationToken)
    {
        Log.DevAlertEmail(
            logger,
            alert.OrganizationId,
            alert.RuleType,
            alert.TargetType,
            alert.TargetEntityId,
            alert.Title,
            alert.Message);

        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "DEV ALERT EMAIL {OrganizationId} {RuleType} {TargetType} {TargetEntityId}: {Title} - {Message}")]
        public static partial void DevAlertEmail(
            ILogger logger,
            Guid organizationId,
            AlertRuleType ruleType,
            string targetType,
            Guid targetEntityId,
            string title,
            string message);
    }
}
