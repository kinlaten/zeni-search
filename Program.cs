using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Services;
using ZeniSearch.Api.Services.Scrapers;
using Hangfire;
using Hangfire.PostgreSql;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//Register DBContext
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Register httpClientFactory
builder.Services.AddHttpClient();

// Register Scraper Services
builder.Services.AddScoped<IProductScraper, TheIconicScraper>();
// builder.Services.AddScoped<IProductScraper, AmazonScraper>(); // Archived

builder.Services.AddScoped<ScraperFactory>();
builder.Services.AddScoped<ScraperService>();


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

var app = builder.Build();

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


// ===== SCHEDULE RECURRING JOBS =======

// Schedule scraper to run daily at 3PM
RecurringJob.AddOrUpdate<ScraperService>(
    "scrape-sandals-daily",
    service => service.ScrapeAllRetailers("sandals"),
    Cron.Daily(15)
);

RecurringJob.AddOrUpdate<ScraperService>(
    "scrape-popular-hourly",
    service => service.ScrapePopularProducts(),
    Cron.Hourly()
);


app.Run();
