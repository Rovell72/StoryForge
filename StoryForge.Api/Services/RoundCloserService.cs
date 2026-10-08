namespace StoryForge.Api.Services;

public sealed class RoundCloserService(
    IServiceScopeFactory scopeFactory,
    IOptions<StoryOptions> options,
    ILogger<RoundCloserService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.RoundCheckIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var closed = await sender.Send(new CloseExpiredRoundsCommand(), stoppingToken);

                if (closed > 0)
                {
                    logger.LogInformation("Закрыто раундов: {Count}", closed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при закрытии раундов");
            }
        }
    }
}
