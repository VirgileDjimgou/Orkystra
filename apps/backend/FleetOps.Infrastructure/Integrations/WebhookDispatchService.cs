using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FleetOps.Core.Modules.Integrations;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure.Integrations;

public sealed partial class WebhookDispatchService(
    FleetOpsDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    IOptions<IntegrationOptions> options,
    ILogger<WebhookDispatchService> logger) : IWebhookDispatchService
{
    public async Task<WebhookDispatchResult> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var settings = options.Value;
        var pending = await dbContext.IntegrationOutboxMessages
            .Where(x => x.Status == IntegrationOutboxStatus.Pending && x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.NextAttemptAtUtc)
            .Take(25)
            .ToListAsync(cancellationToken);

        var delivered = 0;
        var retried = 0;
        var deadLettered = 0;
        if (pending.Count == 0)
        {
            return new WebhookDispatchResult(0, 0, 0);
        }

        var endpointIds = pending.Select(x => x.WebhookEndpointId).Distinct().ToList();
        var endpoints = await dbContext.WebhookEndpoints
            .Where(x => endpointIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var httpClient = httpClientFactory.CreateClient(nameof(WebhookDispatchService));

        foreach (var message in pending)
        {
            if (!endpoints.TryGetValue(message.WebhookEndpointId, out var endpoint))
            {
                message.MarkDeadLetter(now, "Webhook endpoint no longer exists.");
                deadLettered++;
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.TargetUrl)
                {
                    Content = JsonContent.Create(new
                    {
                        deliveryId = message.Id,
                        eventType = message.EventType,
                        aggregateType = message.AggregateType,
                        aggregateId = message.AggregateId,
                        occurredAtUtc = message.OccurredAtUtc,
                        payload = message.PayloadJson
                    })
                };
                var signature = ComputeSignature(endpoint.SigningSecret, message.PayloadJson);
                request.Headers.Add("X-FleetOps-Event", message.EventType);
                request.Headers.Add("X-FleetOps-Delivery", message.Id.ToString());
                request.Headers.Add("X-FleetOps-Signature", signature);

                var response = await httpClient.SendAsync(request, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                dbContext.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt(
                    message.OrganizationId,
                    message.Id,
                    endpoint.Id,
                    message.AttemptCount + 1,
                    (int)response.StatusCode,
                    responseText,
                    response.IsSuccessStatusCode,
                    now));

                if (response.IsSuccessStatusCode)
                {
                    message.MarkDelivered(now);
                    endpoint.MarkSucceeded(now);
                    delivered++;
                }
                else if (message.AttemptCount + 1 >= settings.MaxWebhookAttempts)
                {
                    message.MarkDeadLetter(now, $"HTTP {(int)response.StatusCode}");
                    deadLettered++;
                }
                else
                {
                    var retryAt = now.AddSeconds(settings.RetryBaseDelaySeconds * (message.AttemptCount + 1));
                    message.ScheduleRetry(retryAt, $"HTTP {(int)response.StatusCode}");
                    retried++;
                }
            }
            catch (Exception ex)
            {
                dbContext.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt(
                    message.OrganizationId,
                    message.Id,
                    message.WebhookEndpointId,
                    message.AttemptCount + 1,
                    null,
                    ex.Message,
                    false,
                    now));

                if (message.AttemptCount + 1 >= settings.MaxWebhookAttempts)
                {
                    message.MarkDeadLetter(now, ex.Message);
                    deadLettered++;
                }
                else
                {
                    var retryAt = now.AddSeconds(settings.RetryBaseDelaySeconds * (message.AttemptCount + 1));
                    message.ScheduleRetry(retryAt, ex.Message);
                    retried++;
                }

                Log.WebhookDispatchFailed(
                    logger,
                    ex,
                    message.Id,
                    endpoint.TargetUrl);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new WebhookDispatchResult(delivered, retried, deadLettered);
    }

    public static string ComputeSignature(string secret, string payloadJson)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
        return FormattableString.Invariant($"sha256={Convert.ToHexString(hash).ToLowerInvariant()}");
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Webhook dispatch failed for outbox message {OutboxMessageId} to {TargetUrl}.")]
        public static partial void WebhookDispatchFailed(
            ILogger logger,
            Exception exception,
            Guid outboxMessageId,
            string targetUrl);
    }
}
