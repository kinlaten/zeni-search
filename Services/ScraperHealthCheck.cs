using Hangfire.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ZeniSearch.Api.Services;

public class ScraperHealthCheck : IHealthCheck
{
    private readonly ScraperFactory _factory;
    private readonly ILogger<ScraperHealthCheck> _logger;

    public ScraperHealthCheck(ScraperFactory factory, ILogger<ScraperHealthCheck> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var scrapers = _factory.GetAllScrapers();
            var healthyCount = 0;
            var totalCount = 0;

            foreach (var scraper in scrapers)
            {
                totalCount++;
                try
                {
                    if (await scraper.HealthCheck())
                    {
                        healthyCount++;
                    }
                }
                catch
                {
                    // Count as unhealthy
                }
            }

            if (healthyCount == 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"No scrapers are healthy (0/{totalCount})"
                );
            }

            if (healthyCount < totalCount)
            {
                return HealthCheckResult.Degraded(
                    $"Some scrapers are unhealthy ({healthyCount}/{totalCount})"
                );
            }

            return HealthCheckResult.Healthy(
                $"All scrapers are healthy ({healthyCount}/{totalCount})"
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Healthy check failed");
            return HealthCheckResult.Unhealthy("Health check failed", e);
        }
    }
}