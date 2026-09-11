using Microsoft.Extensions.Options;

namespace MHARS.Web.Services;

/// <summary>
/// Pull model: this service fetches from USGS on a timer and writes to our own database.
/// Citizens' browsers never touch USGS. That keeps page loads fast, keeps the site working
/// if USGS is unreachable, and keeps us well inside their fair-use expectations.
///
/// A BackgroundService is registered as a singleton, but ApplicationDbContext is scoped.
/// Injecting the DbContext directly into the constructor would capture a disposed context
/// after the first request scope ends. IServiceScopeFactory is the fix.
/// </summary>
public class UsgsSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UsgsOptions _options;
    private readonly ILogger<UsgsSyncBackgroundService> _logger;

    public UsgsSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<UsgsOptions> options,
        ILogger<UsgsSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the host finish starting (and any startup migration finish) before the first pull.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.SyncIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        _logger.LogInformation("USGS sync service started. Interval: {Interval} minutes.", interval.TotalMinutes);

        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("USGS sync service stopping.");
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<UsgsEarthquakeService>();
            await service.SyncAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never let a bad cycle kill the timer — the next tick should still fire.
            _logger.LogError(ex, "USGS sync cycle failed; will retry on next tick.");
        }
    }
}
