using System.IO;
using ServiceDelivery.Client.Tests.Maps;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// FE-015 (AC-1, "Use my current location"): the browser geolocation host service imports a UI static
/// JS module to read <c>navigator.geolocation.getCurrentPosition</c>. The host service itself is native
/// (untested, like BrowserTokenStore), but the committed JS module lives in UI and is reachable here —
/// this guards that its source actually exports the function the host service invokes (the same
/// source-read guard pattern GoogleMapComponentTests uses for googleMap.js).
/// </summary>
public class GeolocationModuleArtifactTests
{
    [Fact]
    public void GivenTheGeolocationModule_WhenItsSourceIsRead_ThenItExportsGetCurrentPosition()
    {
        // Arrange
        var modulePath = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.UI", "wwwroot", "Features", "Requester", "geolocation.js");

        // Act
        Assert.True(File.Exists(modulePath), $"Expected geolocation.js at '{modulePath}'.");
        var module = File.ReadAllText(modulePath);

        // Assert
        Assert.Contains("export async function getCurrentPosition", module);
        Assert.Contains("navigator.geolocation.getCurrentPosition", module);
    }
}
