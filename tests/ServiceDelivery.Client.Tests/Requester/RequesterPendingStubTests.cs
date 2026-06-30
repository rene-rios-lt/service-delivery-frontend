using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.UI.Features.Requester.Pages;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// FE-015 forward-reference stub: NavigateToRequesterPending (AC-4) needs a valid destination at
/// /requester/pending. This story creates a placeholder there (full implementation deferred to FE-016);
/// the test pins the placeholder content so the navigation target renders something meaningful.
/// </summary>
public class RequesterPendingStubTests : BunitContext
{
    [Fact]
    public void GivenTheRequesterPendingStub_WhenRendered_ThenItShowsTheFindingTechnicianPlaceholder()
    {
        // Arrange
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Act
        var cut = Render<RequesterPending>();

        // Assert
        var placeholder = cut.Find("[data-testid='requester-pending']");
        Assert.Contains("Finding your technician", placeholder.TextContent);
    }
}
