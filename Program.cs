using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Services;
using ZeniSearch.Api.Services.Scrapers;
using Hangfire;
using Hangfire.PostgreSql;
using Polly;
using Polly.Extensions.Http;
using ZeniSearch.Api.Middleware;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//Register DBContext
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Register httpClientFactory
builder.Services.AddHttpClient("ScraperClient")
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

// Retry Policy: retry 3 times with exponential backoff
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() //auto handle server error 5xx, 408
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) //Add error 429 to list of error that it should trigger retry
        .WaitAndRetryAsync(retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            Console.WriteLine($"Retry {retryCount} after {timespan.TotalSeconds}s delay");
        });
}

// Circuit breaker: Stop trying if too many failures
static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5, //Break after 5 failures
            durationOfBreak: TimeSpan.FromMinutes(1), //Circuit open for 1 minute without attempt retry
            onBreak: (outcome, duration) =>
            {
                Console.WriteLine($"Circuit breaker opened for {duration.TotalSeconds}s");
            },
            onReset: () =>
            {
                Console.WriteLine("Circuit breaker reset");
            }

        );
}

// Register Scraper Services
builder.Services.AddScoped<IProductScraper, TheIconicScraper>();
builder.Services.AddScoped<IProductScraper, BirdsNestScraper>();
// builder.Services.AddScoped<IProductScraper, AmazonScraper>(); // Archived

builder.Services.AddScoped<ScraperFactory>();
builder.Services.AddScoped<ScraperService>();

// Add Playwright browser Service 
builder.Services.AddSingleton<PlaywrightBrowserService>();

builder.Services.AddScoped<PriceHistoryService>();

/* =========================
HANGFIRE CONFIG
*/

// Config Hangfire to use PostgreSql
builder.Services.AddHangfire(config =>
{
    // Use same PostgreSql db
    config.UsePostgreSqlStorage(options =>
    {
        options.UseNpgsqlConnection(connectionString);
    })

    // Config job options
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings();
});

// Add Hangfire server (processes background jobs)
builder.Services.AddHangfireServer(options =>
{
    // Number of workers
    options.WorkerCount = 2;

    // Poll interval (how often to check for new jobs)
    options.SchedulePollingInterval = TimeSpan.FromSeconds(30);
});

//Add Controlers and built-in OpenApi
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database")
                .AddCheck<ScraperHealthCheck>("scraper");

builder.Services.AddMemoryCache();
builder.Services.AddScoped<CachedProductService>();

/* ====================
RUN TIME ENV
*/
var app = builder.Build();

// Init Playwright browser
var playwrightService = app.Services.GetRequiredService<PlaywrightBrowserService>();
await playwrightService.InitializeAsync();

app.UseMiddleware<GlobalExceptionHandler>();

if (app.Environment.IsDevelopment())
{
    // Backend endpoints
    app.MapOpenApi();

    // Add scalar ui for testing
    app.MapScalarApiReference();

    // Hangfire Dashboard
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        //In production, add authentication

        //Add Authorization
        Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
    });

}

app.MapGet("/", () => "Hello world!");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("health");


// ===== SCHEDULE RECURRING JOBS =======

// Schedule scraper to run daily at 3PM
// RecurringJob.AddOrUpdate<ScraperService>(
//     "scrape-sandals-daily",
//     service => service.ScrapeAllRetailers("sandals"),
//     Cron.Daily(15)
// );

// RecurringJob.AddOrUpdate<ScraperService>(
//     "scrape-popular-hourly",
//     service => service.ScrapePopularProducts(),
//     Cron.Hourly()
// );


app.Run();
