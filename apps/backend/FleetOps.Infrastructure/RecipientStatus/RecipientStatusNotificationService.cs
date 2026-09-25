using FleetOps.Core.Modules.Dispatch;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetOps.Infrastructure.RecipientStatus;

public sealed partial class RecipientStatusNotificationService(
    FleetOpsDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider,
    ILogger<RecipientStatusNotificationService> logger) : IRecipientStatusNotificationService
{
    private const int MaxAttempts = 3;

    public async Task QueueMissionStatusAsync(Guid organizationId, Guid missionId, MissionStatus status, CancellationToken cancellationToken)
    {
        var preference = await dbContext.RecipientNotificationPreferences
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
        if (preference?.ConsentGranted != true) return;

        var now = timeProvider.GetUtcNow();
        var links = await dbContext.RecipientStatusLinks
            .Where(x => x.OrganizationId == organizationId && x.MissionId == missionId && x.RecipientEmailProtected != null && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            var key = $"recipient-status:{link.Id:N}:{status}:{preference.Channel}";
            if (dbContext.ChangeTracker.Entries<RecipientStatusNotification>().Any(x => x.State != EntityState.Deleted && x.Entity.OrganizationId == organizationId && x.Entity.DeduplicationKey == key)
                || await dbContext.RecipientStatusNotifications.AnyAsync(x => x.OrganizationId == organizationId && x.DeduplicationKey == key, cancellationToken)) continue;
            dbContext.RecipientStatusNotifications.Add(new RecipientStatusNotification(organizationId, link.Id, missionId, preference.Channel, status.ToString(), key, now));
        }
    }

    public async Task<RecipientStatusNotificationDispatchResult> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var pending = await dbContext.RecipientStatusNotifications
            .Where(x => x.DeliveryStatus == RecipientStatusNotificationDeliveryStatus.Pending && x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.NextAttemptAtUtc).Take(25).ToListAsync(cancellationToken);
        var delivered = 0; var retried = 0; var deadLettered = 0;
        foreach (var notification in pending)
        {
            var preference = await dbContext.RecipientNotificationPreferences.SingleOrDefaultAsync(x => x.OrganizationId == notification.OrganizationId, cancellationToken);
            var link = await dbContext.RecipientStatusLinks.SingleOrDefaultAsync(x => x.Id == notification.RecipientStatusLinkId && x.OrganizationId == notification.OrganizationId, cancellationToken);
            if (preference?.ConsentGranted != true || link?.RecipientEmailProtected is null || !link.IsAvailableAt(now))
            {
                notification.MarkDeadLetter(now, "Recipient notification is no longer permitted or available."); deadLettered++; continue;
            }
            try
            {
                if (preference.IsQuietAt(now))
                {
                    notification.RetryAt(now.AddMinutes(30), "Deferred during configured quiet hours."); retried++; continue;
                }
                var recipient = dataProtectionProvider.CreateProtector("FleetOps.RecipientStatus.Email.v1").Unprotect(link.RecipientEmailProtected);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    Log.RecipientStatusNotificationDelivered(logger, notification.Id, notification.OrganizationId, notification.Status, preference.Language);
                }
                notification.MarkDelivered(now); delivered++;
            }
            catch (Exception ex)
            {
                if (notification.AttemptCount + 1 >= MaxAttempts) { notification.MarkDeadLetter(now, ex.Message); deadLettered++; }
                else { notification.RetryAt(now.AddMinutes(5 * (notification.AttemptCount + 1)), ex.Message); retried++; }
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RecipientStatusNotificationDispatchResult(delivered, retried, deadLettered);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 23, Level = LogLevel.Information, Message = "Recipient status notification {NotificationId} delivered for organization {OrganizationId}; status={Status}, language={Language}.")]
        public static partial void RecipientStatusNotificationDelivered(ILogger logger, Guid notificationId, Guid organizationId, string status, string language);
    }
}
