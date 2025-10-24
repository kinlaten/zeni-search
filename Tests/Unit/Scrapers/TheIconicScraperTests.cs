using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ZeniSearch.Api.Services.Scrapers;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;

namespace ZeniSearch.Api.Tests.Unit.Scrapers;

public class TheIconicScraperTests
{
    private readonly Mock<ILogger<TheIconicScraper>> _mockLogger;
    private readonly TheIconicScraper _scraper;

    public TheIconicScraperTests()
    {
        // For unit tests, we skip database mocking - focus on HTTP/parsing logic only
        _mockLogger = new Mock<ILogger<TheIconicScraper>>();

        // Create a mock DbContext with minimal setup to avoid constructor issues
        var mockContext = CreateMockAppDbContext();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();

        _scraper = new TheIconicScraper(mockContext, mockHttpClientFactory.Object, _mockLogger.Object);
    }

    /// <summary>
    /// Creates a mock AppDbContext that doesn't try to initialize the real database
    /// </summary>
    private AppDbContext CreateMockAppDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void RetailerName_ShouldReturn_TheIconic()
    {
        // Arrange & Act
        var retailerName = _scraper.RetailerName;

        // Assert
        Assert.Equal("The Iconic", retailerName);
    }

    [Fact]
    public async Task HealthCheck_WithValidResponse_ShouldReturnTrue()
    {
        // Arrange
        var mockHttpClient = new Mock<HttpClient>();
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Test</body></html>")
        };

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(mockHttpClient.Object);

        var scraper = new TheIconicScraper(CreateMockAppDbContext(), mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await scraper.HealthCheck();

        // Assert
        Assert.True(result);
    }
}
