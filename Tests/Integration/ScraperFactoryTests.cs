using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZeniSearch.Api.Services;
using ZeniSearch.Api.Services.Scrapers;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ZeniSearch.Api.Tests.Integration;

public class ScraperFactoryTests
{
    [Fact]
    public void GetAllScrapers_WithRegisteredScrapers_ShouldReturnAll()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Mock scrapers
        var mockScraper1 = new Mock<IProductScraper>();
        mockScraper1.Setup(s => s.RetailerName).Returns("The Iconic");

        var mockScraper2 = new Mock<IProductScraper>();
        mockScraper2.Setup(s => s.RetailerName).Returns("Amazon Au");

        // Register mock scrapers
        services.AddScoped<IProductScraper>(_ => mockScraper1.Object);
        services.AddScoped<IProductScraper>(_ => mockScraper2.Object);
        services.AddScoped<ScraperFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<ScraperFactory>();

        // Act
        var scrapers = factory.GetAllScrapers().ToList();

        // Assert
        Assert.Equal(2, scrapers.Count);
        Assert.Contains(scrapers, s => s.RetailerName == "The Iconic");
        Assert.Contains(scrapers, s => s.RetailerName == "Amazon Au");
    }

    [Fact]
    public void GetService_WithValidRetailerName_ShouldReturnCorrectScraper()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var mockScraper = new Mock<IProductScraper>();
        mockScraper.Setup(s => s.RetailerName).Returns("The Iconic");

        services.AddScoped<IProductScraper>(_ => mockScraper.Object);
        services.AddScoped<ScraperFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<ScraperFactory>();

        // Act
        var scraper = factory.GetService("The Iconic");

        // Assert
        Assert.NotNull(scraper);
        Assert.Equal("The Iconic", scraper.RetailerName);
    }

    [Fact]
    public void GetService_WithInvalidRetailerName_ShouldReturnNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var mockScraper = new Mock<IProductScraper>();
        mockScraper.Setup(s => s.RetailerName).Returns("The Iconic");

        services.AddScoped<IProductScraper>(_ => mockScraper.Object);
        services.AddScoped<ScraperFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<ScraperFactory>();

        // Act
        var scraper = factory.GetService("NonExistentRetailer");

        // Assert
        Assert.Null(scraper);
    }

    [Fact]
    public async Task GetHealthyScrapers_ShouldReturnOnlyHealthyOnes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var healthyScraper = new Mock<IProductScraper>();
        healthyScraper.Setup(s => s.RetailerName).Returns("Healthy Scraper");
        healthyScraper.Setup(s => s.HealthCheck()).ReturnsAsync(true);

        var unhealthyScraper = new Mock<IProductScraper>();
        unhealthyScraper.Setup(s => s.RetailerName).Returns("Unhealthy Scraper");
        unhealthyScraper.Setup(s => s.HealthCheck()).ReturnsAsync(false);

        services.AddScoped<IProductScraper>(_ => healthyScraper.Object);
        services.AddScoped<IProductScraper>(_ => unhealthyScraper.Object);
        services.AddScoped<ScraperFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<ScraperFactory>();

        // Act
        var healthyScrapers = (await factory.GetHealthyScrapers()).ToList();

        // Assert
        Assert.Single(healthyScrapers);
        Assert.Equal("Healthy Scraper", healthyScrapers[0].RetailerName);
    }
}
