using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.Maps.Services;

namespace ServiceDelivery.Client.Tests.Maps;

public class ConfigurationMapsKeyProviderTests
{
    private static IConfiguration ConfigWith(string? apiKey)
    {
        var values = new Dictionary<string, string?> { ["GoogleMaps:ApiKey"] = apiKey };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void GivenConfigWithApiKey_WhenGetMapsApiKeyCalled_ThenKeyIsReturned()
    {
        // Arrange
        IMapsKeyProvider provider = new ConfigurationMapsKeyProvider(ConfigWith("AIza-from-config"));

        // Act
        var key = provider.GetMapsApiKey();

        // Assert
        Assert.Equal("AIza-from-config", key);
    }

    [Fact]
    public void GivenConfigWithoutApiKey_WhenGetMapsApiKeyCalled_ThenNullIsReturned()
    {
        // Arrange — the committed appsettings.json placeholder is empty, so a clean checkout (no
        // gitignored appsettings.Local.json) yields no key. The provider must surface that as null
        // rather than throwing, so MapsLoader's blank-key guard (AC-3) can take over.
        var emptyConfig = new ConfigurationBuilder().Build();
        IMapsKeyProvider provider = new ConfigurationMapsKeyProvider(emptyConfig);

        // Act
        var key = provider.GetMapsApiKey();

        // Assert
        Assert.Null(key);
    }
}
