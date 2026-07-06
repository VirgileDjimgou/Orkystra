using Microsoft.Extensions.Options;

namespace Orkystra.Api.Eventing;

public sealed class OutboxRecoveryWorker : BackgroundService
{
    private readonly EventBackboneOptions _options;
    private readonly OutboxRecoveryService _recoveryService;
    private readonly ILogger<OutboxRecoveryWorker> _logger;

    public OutboxRecoveryWorker(
        IOptions<EventBackboneOptions> options,
        OutboxRecoveryService recoveryService,
        ILogger<OutboxRecoveryWorker> logger)
    {
        _options = options.Value;
        _recoveryService = recoveryService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.AutoReplayEnabled)
        {
            _logger.LogInformation("Outbox auto-recovery worker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _recoveryService.ReplayPendingAsync(
                    _options.AutoReplayBatchSize,
                    triggeredAutomatically: true,
                    stoppingToken);

                if (result.Total > 0)
                {
                    _logger.LogInformation(
                        "Outbox auto-recovery processed {Total} entries: {Replayed} replayed, {Failed} failed.",
                        result.Total,
                        result.Replayed,
                        result.Failed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Outbox auto-recovery loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.AutoReplayIntervalSeconds)), stoppingToken);
        }
    }
}
