using System.Security.Principal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Polly;

namespace ZeniSearch.Api.Services;

public class PlaywrightBrowserService : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly ILogger<PlaywrightBrowserService> _logger;

    public PlaywrightBrowserService(ILogger<PlaywrightBrowserService> logger)
    {
        _logger = logger;
    }

    // Init Playwright and launch browser
    public async Task InitializeAsync()
    {
        try
        {
            _playwright = await Playwright.CreateAsync();

            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true //Run in background, no UI
            });

            _logger.LogInformation("Playwright browser initiated successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate Playwright browser");
            throw;
        }
    }

    // Create a new page wih proper headers for Birds Nest 
    public async Task<IPage> CreatePageAsync()
    {
        if (_browser == null)
        {
            throw new InvalidOperationException("Browser not initialized. Call InitializeAsync first");
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Mobile Safari/537.36",
            ExtraHTTPHeaders = new Dictionary<string, string>{
                {"accept", "*/*"},
                {"accept-language","en-GB,en,q=0.9"},
                {"dnt","1"},
                {"origin","https://www.birdsnest.com.au"},
                {"referer","https://www.birdsnest.com.au"}
            }
        });
        var page = await context.NewPageAsync();
        return page;
    }


    // Fetch content from URL using browser automation
    public async Task<string> FetchPageContentAsync(string url, int timeoutMs = 30000)
    {
        IPage? page = null;
        try
        {
            page = await CreatePageAsync();

            _logger.LogInformation("Navigating to: {Url}", url);

            // Navigate to page
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = timeoutMs
            });

            // Wait a bit for nay dynamic content to load
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await Task.Delay(2000);

            string content = await page.ContentAsync();
            _logger.LogInformation("Successfully fetched page content ({ByteCount} bytes)", content.Length);

            return content;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to fetch page content from {Url}", url);
            throw;
        }
        finally
        {
            if (page != null)
            {
                await page.CloseAsync();
            }
        }
    }

    public async Task<string> FetchApiResponseAsync(string url, string apiUrlPattern)
    {
        IPage? page = null;

        try
        {
            page = await CreatePageAsync();

            _logger.LogInformation("Setting up API interception for pattern: {Pattern}", apiUrlPattern);

            // Start listening for the API response
            var apiResponseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains(apiUrlPattern) && response.Status == 200);

            // Nav to page
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

            // Wait for API response
            var response = await apiResponseTask;
            string content = await response.TextAsync();

            _logger.LogInformation("Successfully captured API response ({ByteCount} bytes)", content.Length);

            return content;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to fetch API response form {Url}", url);
            throw;
        }
        finally
        {
            if (page != null)
            {
                await page.CloseAsync();
            }
        }
    }

    // Dispose resources
    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }
}