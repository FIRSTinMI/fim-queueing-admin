using fim_queueing_admin.Data;
using Microsoft.EntityFrameworkCore;

namespace fim_queueing_admin.Services;

/// <summary>
/// PlanetScale will shut down our database if no connections are made to it for extended periods of time. This service
/// just does a simple <c>SELECT 1;</c> every 3 hours to keep it up and running.
/// </summary>
public class DatabaseKeepAliveService(ILogger<DatabaseKeepAliveService> logger, IServiceProvider serviceProvider)
    : IHostedService, IDisposable
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromHours(3));

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Database Keep Alive service running.");

        do
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FimDbContext>();
            
            logger.LogInformation("Keeping database alive.");
            await dbContext.Database.ExecuteSqlAsync($"SELECT 1;", stoppingToken);
        } while (!stoppingToken.IsCancellationRequested && await _timer.WaitForNextTickAsync(stoppingToken));
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Database Keep Alive service stopping.");

        _timer.Dispose();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}