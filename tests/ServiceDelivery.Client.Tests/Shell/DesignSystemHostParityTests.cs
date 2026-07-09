using System.IO;
using ServiceDelivery.Client.Tests.Maps;

namespace ServiceDelivery.Client.Tests.Shell;

/// <summary>
/// QUAL-011 AC-2 host-parity guard. The consolidated design-system.css is a Razor Class Library static
/// asset served at <c>_content/ServiceDelivery.Client.UI/design-system.css</c> (the same mechanism as
/// MudBlazor.min.css). Each host ships its OWN wwwroot/index.html, so the &lt;link&gt; must be added to
/// all three (Web / Desktop / Mobile) in the same change — a stylesheet linked on only one host would
/// leave the shared sd-* tokens unstyled on the other two (the BUG-020 / BUG-022 host-parity defect
/// class). These source-read guards assert every host page links the sheet.
/// </summary>
public class DesignSystemHostParityTests
{
    private const string DesignSystemLink = "_content/ServiceDelivery.Client.UI/design-system.css";

    private static string HostIndexHtml(string hostProject) => File.ReadAllText(
        RepoRoot.Combine("src", hostProject, "wwwroot", "index.html"));

    [Fact]
    public void GivenWebHostIndexHtml_WhenChecked_ThenDesignSystemCssLinkIsPresent()
    {
        // Arrange
        var html = HostIndexHtml("ServiceDelivery.Client.Web");

        // Act & Assert
        Assert.Contains(DesignSystemLink, html);
    }

    [Fact]
    public void GivenDesktopHostIndexHtml_WhenChecked_ThenDesignSystemCssLinkIsPresent()
    {
        // Arrange
        var html = HostIndexHtml("ServiceDelivery.Client.Desktop");

        // Act & Assert
        Assert.Contains(DesignSystemLink, html);
    }

    [Fact]
    public void GivenMobileHostIndexHtml_WhenChecked_ThenDesignSystemCssLinkIsPresent()
    {
        // Arrange
        var html = HostIndexHtml("ServiceDelivery.Client.Mobile");

        // Act & Assert
        Assert.Contains(DesignSystemLink, html);
    }
}
