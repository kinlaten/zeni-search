namespace ZeniSearch.Api.Services;

public class ScraperMonitor
{
    private readonly ILogger<ScraperMonitor> _logger;
    public ScraperMonitor(ILogger<ScraperMonitor> logger)
    {
        _logger = logger;
    }

    // Monitors scrapers execution and logs issues 
    public async Task<T> MonitorScraperExecution<T>(
        string scraperName,
        Func<Task<T>> scraperAction
    )
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting {Scraper}", scraperName);

            var result = await scraperAction();

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            _logger.LogInformation("{Scraper} completed in {Duration}s",
            scraperName,
            Math.Round(duration, 2));

            return result;
        }
        catch (Exception e)
        {
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            _logger.LogError(e, "{Scraper} failed after {Duration}s",
            scraperName,
            Math.Round(duration, 2));

            // Send Alert (email, slack...)
            await SendAlert(scraperName, e);

            throw;
        }
    }

    private async Task SendAlert(string scraperName, Exception e)
    {
        // Todo
        // -Send Email
        // -Log to monitoring service (Grafana)

        _logger.LogWarning("ALERT: {Scraper} failed with error: {Message}",
        scraperName,
        e.Message);

        await Task.CompletedTask;
    }

}