using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ZeniSearch.Api.Services.Scrapers;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Models;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;

namespace ZeniSearch.Api.Tests.Unit.Scrapers;

/// <summary>
/// Tests specific to TheIconicScraper implementation
/// </summary>
public class TheIconicScraperTests : BaseScraperTests<TheIconicScraper>
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public TheIconicScraperTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
    }

    protected override TheIconicScraper CreateScraper()
    {
        var mockContext = CreateMockAppDbContext();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        return new TheIconicScraper(mockContext, mockHttpClientFactory.Object, MockLogger.Object);
    }

    // ========================
    // SPECIFIC TESTS (TheIconic Only)
    // ========================

    [Fact]
    public void RetailerName_ShouldBeTheIconic()
    {
        // Act
        var retailerName = Scraper.RetailerName;

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

        var scraper = new TheIconicScraper(CreateMockAppDbContext(), mockHttpClientFactory.Object, MockLogger.Object);

        // Act
        var result = await scraper.HealthCheck();

        // Assert
        Assert.True(result);
    }

    // TODO: Add more TheIconic-specific tests here
    // For example:
    // - Test parsing The Iconic's specific HTML structure
    // - Test handling of The Iconic's price format
    // - Test The Iconic's product URL format
}

