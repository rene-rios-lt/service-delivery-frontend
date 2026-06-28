using System.IO;
using System.Text.Json;

namespace ServiceDelivery.Client.Tests.Maps;

// Config-AC tests for FE-025 (AC-4 documentation deliverable + AC-5 committed placeholder / gitignore).
// These assert the committed artifacts that keep the real key out of source control: the empty placeholder
// in appsettings.json, the gitignore entry for appsettings.Local.json, and the maps-api-key.md doc.
public class MapsKeyConfigArtifactTests
{
    [Fact]
    public void GivenTheCommittedAppSettingsJson_WhenParsed_ThenGoogleMapsApiKeyExistsAndIsEmpty()
    {
        // Arrange
        var appSettingsPath = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.Web", "wwwroot", "appsettings.json");

        // Act
        using var doc = JsonDocument.Parse(File.ReadAllText(appSettingsPath));

        // Assert
        Assert.True(
            doc.RootElement.TryGetProperty("GoogleMaps", out var googleMaps),
            "appsettings.json must contain a GoogleMaps section.");
        Assert.True(
            googleMaps.TryGetProperty("ApiKey", out var apiKey),
            "GoogleMaps section must contain an ApiKey entry.");
        Assert.Equal(string.Empty, apiKey.GetString());
    }

    [Fact]
    public void GivenTheRepository_WhenDocFileRead_ThenItContainsGoogleMapsApiKeyName()
    {
        // Arrange
        var docPath = RepoRoot.Combine("docs", "maps-api-key.md");

        // Act
        var doc = File.ReadAllText(docPath);

        // Assert
        Assert.Contains("GoogleMaps:ApiKey", doc);
    }

    [Fact]
    public void GivenTheRepository_WhenDocFileRead_ThenItContainsBlazorWebViewRestrictionNote()
    {
        // Arrange — AC-4's documentation half: the doc must record the BlazorWebView / iOS origin caveat
        // from ADR-0010 and the concrete iOS restriction (the real Mobile bundle id) used to satisfy it.
        var docPath = RepoRoot.Combine("docs", "maps-api-key.md");

        // Act
        var doc = File.ReadAllText(docPath);

        // Assert
        Assert.Contains("BlazorWebView", doc);
        Assert.Contains("com.companyname.servicedelivery.client.mobile", doc);
    }

    [Fact]
    public void GivenTheGitignoreFile_WhenRead_ThenItContainsAppSettingsLocalJson()
    {
        // Arrange
        var gitignorePath = RepoRoot.Combine(".gitignore");

        // Act
        var gitignore = File.ReadAllText(gitignorePath);

        // Assert
        Assert.Contains("appsettings.Local.json", gitignore);
    }
}
