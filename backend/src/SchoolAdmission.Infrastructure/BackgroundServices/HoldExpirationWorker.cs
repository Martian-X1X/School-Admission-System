using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolAdmission.Application.Interfaces;

namespace SchoolAdmission.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically persists the EXPIRED status for overdue HELD rows in bounded batches.
///
/// This worker only updates the stored status for reporting; it is never the
/// authority for whether a reservation is still valid. Occupancy counts and
/// confirmation logic independently compare expires_at against clock_timestamp().
/// </summary>
public sealed class HoldExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HoldExpirationWorker> _logger;
    private readonly ExpirationSettings _settings;

    public HoldExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ExpirationSettings> settings,
        ILogger<HoldExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Hold expiration worker started (scan every {Interval}s, batch size {Batch}).",
            _settings.ScanIntervalSeconds,
            _settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var totalExpired = 0;
                int batchExpired;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var holds = scope.ServiceProvider.GetRequiredService<IHoldRepository>();

                    do
                    {
                        batchExpired = await holds.ExpireOverdueAsync(_settings.BatchSize, stoppingToken);
                        totalExpired += batchExpired;
                    }
                    while (batchExpired >= _settings.BatchSize);
                }

                if (totalExpired > 0)
                {
                    _logger.LogInformation("Released {Count} expired seat hold(s).", totalExpired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Hold expiration sweep failed; retrying next interval.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.ScanIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Hold expiration worker stopped.");
    }
}
