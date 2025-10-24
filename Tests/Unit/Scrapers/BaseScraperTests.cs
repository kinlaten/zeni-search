using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ZeniSearch.Api.Services;
using ZeniSearch.Api.Data;
using System;
using Microsoft.EntityFrameworkCore;

namespace ZeniSearch.Api.Tests.Unit.Scrapers;

/// <summary>
/// Base template for testing any scraper that implements IProductScraper.
/// All scrapers MUST pass these tests to ensure interface compliance.
/// 
/// USAGE:
/// ------
/// Create a test class that inherits from this:
/// 
///     public class YourScraperTests : BaseScraperTests<YourScraper>
///     {
///         protected override YourScraper CreateScraper()
///         {
///             // Return an instance of your scraper
///         }
///         
///         // Add your scraper-specific tests here
///     }
/// 
/// This automatically gives you 3 common tests:
/// ✓ RetailerName_ShouldNotBeEmpty
/// ✓ RetailerName_ShouldBeConsistent
/// ✓ HealthCheck_ShouldReturnBoolean (validates return type, not behavior)
/// 
/// NOTE: Tests that require actual HTTP/scraping are left to specific implementations
/// because mocking these requires retailer-specific knowledge.
/// </summary>
public abstract class BaseScraperTests<TScraper> where TScraper : IProductScraper
{
    protected readonly Mock<ILogger<TScraper>> MockLogger;
    protected TScraper Scraper;

    public BaseScraperTests()
    {
        MockLogger = new Mock<ILogger<TScraper>>();
        Scraper = CreateScraper();
    }

    /// <summary>
    /// Each scraper test class must implement this to create an instance for testing.
    /// </summary>
    protected abstract TScraper CreateScraper();

    /// <summary>
    /// Helper to create a mock AppDbContext using in-memory database.
    /// This avoids database initialization issues during testing.
    /// </summary>
    protected AppDbContext CreateMockAppDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ========================
    // COMMON TESTS (All Scrapers Must Pass)
    // ========================

    /// <summary>
    /// Ensures the scraper has a non-empty retailer name.
    /// This is a simple property check, not a behavior test.
    /// </summary>
    [Fact]
    public void RetailerName_ShouldNotBeEmpty()
    {
        // Act
        var name = Scraper.RetailerName;

        // Assert
        Assert.NotNull(name);
        Assert.NotEmpty(name);
    }

    /// <summary>
    /// Ensures the scraper's retailer name is consistent (doesn't change).
    /// </summary>
    [Fact]
    public void RetailerName_ShouldBeConsistent()
    {
        // Act
        var name1 = Scraper.RetailerName;
        var name2 = Scraper.RetailerName;

        // Assert
        Assert.Equal(name1, name2);
    }

    /// <summary>
    /// Ensures HealthCheck returns a boolean value.
    /// NOTE: This only validates that the method exists and returns bool.
    /// Actual health check behavior is tested in scraper-specific tests.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task HealthCheck_ShouldReturnBoolean()
    {
        // Act
        var result = await Scraper.HealthCheck();

        // Assert
        Assert.IsType<bool>(result);
    }

    // ========================
    // NOTES ON TESTING
    // ========================

    // ScrapeProducts() tests are NOT included here because:
    // 1. They require proper HttpClientFactory mocking
    // 2. Each scraper has different HTML structure
    // 3. Mocking HTTP is retailer-specific
    // 4. These should be in scraper-specific test files
    //
    // Examples of scraper-specific tests:
    // - ScrapeProducts_WithValidSearchTerm_ShouldReturnInt
    // - ScrapeProducts_WithEmptySearchTerm_ShouldHandleGracefully
    // - ScrapeProducts_WithNegativeMaxProducts_ShouldHandleGracefully
    //
    // See TheIconicScraperTests.cs or ScraperTestsTemplate.cs for examples.
}
