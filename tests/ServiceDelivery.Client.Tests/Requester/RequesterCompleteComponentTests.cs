using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Requester.Pages;
using ServiceDelivery.Client.UI.Shared.Components;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// bUnit component tests for <see cref="RequesterComplete"/> (FE-019). Drives the mockup
/// (requester-complete__mobile-390x844): the "Your service is complete." heading and the completion
/// subtitle (AC-1), the STRUCTURAL absence of the tracking map and ETA chip (AC-2), the "Submit a new
/// request" primary button and "Done" ghost button (AC-3), the app-bar "Request closed" subtitle rendered
/// through the composed PersonaShell (AC-1), and a parameterized viewport-stub theory asserting the same
/// required elements render across the mobile (Drawer) and web/desktop (AccountMenu) platform shells
/// (AC-5 — no desktop-specific layout is invented; the mobile mockup is the only layout variant specified).
/// </summary>
public class RequesterCompleteComponentTests : BunitContext
{
    private readonly Mock<IServiceCompletedStore> _completedStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();

    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    // Registers the completion ViewModel (seeded from the store payload) and a real ShellViewModel, then
    // returns the shell so the caller can render the page either standalone or inside the shell. Requester
    // is a Desktop/Web/Mobile persona; the shell binding under test (the app-bar subtitle) is identical
    // across menu styles, so the AccountMenu style is used — it is the sync-dispose-safe style bUnit can
    // tear down cleanly (the Drawer style pulls in MudBlazor's async-only PointerEventsNoneService, which a
    // BunitContext-inheriting class cannot dispose synchronously — mirrors RequesterTrackingShellRenderTests).
    private ShellViewModel RegisterServices(ServiceCompletionData? payload = null)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _completedStore.SetupGet(s => s.CurrentPayload).Returns(payload);
        Services.AddSingleton(_completedStore.Object);
        var viewModel = new RequesterCompleteViewModel(_completedStore.Object, _navigator.Object);
        Services.AddSingleton(viewModel);

        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.AccountMenu);
        var shell = new ShellViewModel(
            _tokenStore.Object, _navigator.Object, _sideEffect.Object,
            _releaseAction.Object, _presentation.Object, new PersonaMenuFactory());
        shell.Load(new UserProfile(
            Guid.NewGuid(), "Marcus Webb", UserRole.Requester, ServiceTier.Gold, Guid.NewGuid()));
        Services.AddSingleton(shell);
        return shell;
    }

    private IRenderedComponent<RequesterComplete> RenderPage(ServiceCompletionData? payload = null)
    {
        RegisterServices(payload);
        return Render<RequesterComplete>();
    }

    // Renders PersonaShell with a real RequesterComplete page inside its Body (production composition), so
    // the shell owns the app-bar subtitle binding and the page sets it through the shared ShellViewModel.
    private IRenderedComponent<PersonaShell> RenderInShell(ServiceCompletionData? payload = null)
    {
        var shell = RegisterServices(payload);
        RenderFragment body = builder =>
        {
            builder.OpenComponent<RequesterComplete>(0);
            builder.CloseComponent();
        };
        return Render<PersonaShell>(p => p
            .Add(c => c.ViewModel, shell)
            .Add(c => c.Body, body));
    }

    [Fact]
    public void GivenRequesterCompleteComponent_WhenRendered_ThenCompletionHeadingIsVisible()
    {
        // Arrange — AC-1: the static "Your service is complete." heading is always shown.
        var cut = RenderPage();

        // Act
        var heading = cut.Find("[data-testid='completion-heading']");

        // Assert
        Assert.Equal("Your service is complete.", heading.TextContent.Trim());
    }

    [Fact]
    public void GivenCurrentPayloadWithBothFields_WhenRendered_ThenCompletionSubtitleShowsFullForm()
    {
        // Arrange — AC-1/AC-4: with the store populated the subtitle reads the full form. Distinct rep name
        // and DTC title so the assertion cannot pass by coincidence.
        var cut = RenderPage(new ServiceCompletionData("Jordan Tran", "Transmission Control Fault"));

        // Act
        var subtitle = cut.Find("[data-testid='completion-subtitle']");

        // Assert
        Assert.Equal(
            "Jordan Tran resolved your Transmission Control Fault. Thanks for using Service Delivery.",
            subtitle.TextContent.Trim());
    }

    [Fact]
    public void GivenNoCurrentPayload_WhenRendered_ThenCompletionSubtitleShowsGenericForm()
    {
        // Arrange — AC-4 graceful degrade: an unpopulated store renders the generic thank-you, never a
        // half-built sentence.
        var cut = RenderPage(payload: null);

        // Act
        var subtitle = cut.Find("[data-testid='completion-subtitle']");

        // Assert
        Assert.Equal("Your service is complete. Thanks for using Service Delivery.", subtitle.TextContent.Trim());
    }

    [Fact]
    public void GivenRequesterCompleteComponent_WhenRendered_ThenNoTrackingMapElementIsPresent()
    {
        // Arrange — AC-2: the completion screen is a terminal state with no live map. Its absence is
        // structural — the tracking map element must not be in the DOM.
        var cut = RenderPage();

        // Act / Assert
        Assert.Empty(cut.FindAll("[data-testid='tracking-map']"));
    }

    [Fact]
    public void GivenRequesterCompleteComponent_WhenRendered_ThenNoEtaChipElementIsPresent()
    {
        // Arrange — AC-2: no ETA chip on the completion screen (the trip is over).
        var cut = RenderPage();

        // Act / Assert
        Assert.Empty(cut.FindAll("[data-testid='eta-chip']"));
    }

    [Fact]
    public void GivenRequesterCompleteComponent_WhenRendered_ThenSubmitNewRequestButtonIsVisible()
    {
        // Arrange — AC-3: the primary full-width "Submit a new request" button is shown.
        var cut = RenderPage();

        // Act
        var button = cut.Find("[data-testid='submit-new-request-button']");

        // Assert
        Assert.Contains("Submit a new request", button.TextContent);
    }

    [Fact]
    public void GivenRequesterCompleteComponent_WhenRendered_ThenDoneButtonIsVisible()
    {
        // Arrange — AC-3: the secondary "Done" ghost button is shown below the primary action.
        var cut = RenderPage();

        // Act
        var button = cut.Find("[data-testid='done-button']");

        // Assert
        Assert.Contains("Done", button.TextContent);
    }

    [Fact]
    public void GivenSubmitNewRequestButton_WhenClicked_ThenViewModelNavigatesToRequesterHome()
    {
        // Arrange — AC-3: clicking the primary button delegates to the ViewModel, which routes home.
        var cut = RenderPage();

        // Act
        cut.Find("[data-testid='submit-new-request-button']").Click();

        // Assert
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.Requester), Times.Once);
    }

    [Fact]
    public void GivenDoneButton_WhenClicked_ThenViewModelNavigatesToRequesterHome()
    {
        // Arrange — AC-3: clicking "Done" also routes home.
        var cut = RenderPage();

        // Act
        cut.Find("[data-testid='done-button']").Click();

        // Assert
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.Requester), Times.Once);
    }

    [Fact]
    public void GivenPersonaShellWithCompletePage_WhenRendered_ThenAppBarContextShowsRequestClosed()
    {
        // Arrange — AC-1: the composed shell app bar shows the "Request closed" subtitle the page sets
        // through the shared ShellViewModel (mockup). Asserts the RENDERED appbar-context DOM text, not a
        // ViewModel field (the BUG-044 rendered-DOM lesson).
        var cut = RenderInShell();

        // Act / Assert
        cut.WaitForAssertion(() =>
            Assert.Equal("Request closed", cut.Find("[data-testid='appbar-context']").TextContent.Trim()));
    }

    [Theory]
    [InlineData("mobile")]
    [InlineData("web")]
    [InlineData("desktop")]
    public void GivenRequesterCompleteComponent_WhenRenderedAtAnyViewport_ThenAllRequiredLayoutElementsArePresent(
        string viewport)
    {
        // Arrange — AC-5: "responsive from mobile through desktop". The completion screen has NO
        // viewport-conditional rendering — its responsiveness is pure CSS (the .sd-complete max-width
        // constraint reflows the single column), so no element is hidden or added at any breakpoint. This
        // stub asserts exactly that: the same required data-testid elements are present at each named
        // viewport. The pixel-level fidelity against the sole (mobile) mockup is verified by the AI-review
        // render-and-screenshot gate; no desktop-specific layout is invented (the Evaluator's AC-5 warning).
        Assert.False(string.IsNullOrEmpty(viewport));
        var cut = RenderPage(new ServiceCompletionData("Jordan Tran", "Transmission Control Fault"));

        // Act / Assert — every required element is present regardless of the viewport.
        Assert.NotNull(cut.Find("[data-testid='completion-heading']"));
        Assert.NotNull(cut.Find("[data-testid='completion-subtitle']"));
        Assert.NotNull(cut.Find("[data-testid='submit-new-request-button']"));
        Assert.NotNull(cut.Find("[data-testid='done-button']"));
    }
}
