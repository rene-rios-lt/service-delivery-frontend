using Bunit;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Dispatcher.Components;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// FE-005 bUnit coverage for the redirect UI: the Redirect button attached to <see cref="RequestCard"/>
/// (AC-1/AC-4/AC-5) and the <see cref="RedirectConfirmDialog"/> content and states (AC-2/AC-3/AC-4). All
/// assertions target <c>data-testid</c> selectors and rendered text from the injected parameters — the mockup
/// (<c>dispatcher-redirect__desktop-1440x900.png</c>) governs the labels and the CURRENT JOB → NEW JOB swap.
/// </summary>
public class RequestCardRedirectTests : BunitContext
{
    private static readonly Guid RequestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");
    private static readonly Guid RepId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    private static ActiveRequestEntry Entry() =>
        new(RequestId, "Marcus Webb", ServiceTier.Gold, "Transmission Control Fault", "Pending", null,
            DateTimeOffset.UtcNow.AddMinutes(-1));

    private static RedirectInfo Info(bool inCooldown = false, ServiceTier newTier = ServiceTier.Gold) =>
        new(RepId, "J. Tran", ServiceTier.Silver, "Hydraulic Pressure Loss", newTier,
            "Transmission Control Fault", inCooldown, RequestId);

    // ---- AC-1 / AC-4 / AC-5: the Redirect button on the card --------------------------------------------

    [Fact]
    public void GivenRequestCardWithRedirectInfo_WhenRendered_ThenRedirectButtonVisible()
    {
        // Arrange & Act
        var cut = Render<RequestCard>(p => p
            .Add(c => c.Entry, Entry())
            .Add(c => c.Redirect, Info()));

        // Assert
        var button = cut.Find($"[data-testid='redirect-btn-{RequestId}']");
        Assert.Contains("Redirect", button.TextContent);
    }

    [Fact]
    public void GivenRequestCardWithNullRedirectInfo_WhenRendered_ThenNoRedirectButton()
    {
        // Arrange & Act — no eligible rep: the button must not render.
        var cut = Render<RequestCard>(p => p
            .Add(c => c.Entry, Entry())
            .Add(c => c.Redirect, (RedirectInfo?)null));

        // Assert
        Assert.Empty(cut.FindAll($"[data-testid='redirect-btn-{RequestId}']"));
    }

    [Fact]
    public void GivenErrorState_WhenRequestCardRendered_ThenRedirectButtonDisabled()
    {
        // Arrange & Act — mid-flight (IsRedirecting) the button is disabled (optimistic loading state, AC-3/AC-4).
        var cut = Render<RequestCard>(p => p
            .Add(c => c.Entry, Entry())
            .Add(c => c.Redirect, Info())
            .Add(c => c.IsRedirecting, true));

        // Assert
        var button = cut.Find($"[data-testid='redirect-btn-{RequestId}']");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task GivenRequestCardWithRedirectInfo_WhenButtonClicked_ThenOnRedirectClickedFiresWithRequestId()
    {
        // Arrange
        Guid? clicked = null;
        var cut = Render<RequestCard>(p => p
            .Add(c => c.Entry, Entry())
            .Add(c => c.Redirect, Info())
            .Add(c => c.OnRedirectClicked, (Guid id) => clicked = id));

        // Act
        await cut.Find($"[data-testid='redirect-btn-{RequestId}']").ClickAsync(new());

        // Assert
        Assert.Equal(RequestId, clicked);
    }

    // ---- AC-2 / AC-3 / AC-4: the confirmation dialog ----------------------------------------------------

    private IRenderedComponent<RedirectConfirmDialog> RenderDialog(
        RedirectInfo info, bool isRedirecting = false, string? error = null) =>
        Render<RedirectConfirmDialog>(p => p
            .Add(c => c.ActiveRedirectInfo, info)
            .Add(c => c.IsRedirecting, isRedirecting)
            .Add(c => c.RedirectError, error));

    [Fact]
    public void GivenRedirectConfirmDialog_WhenRenderedWithRepAndJobDetails_ThenAllFieldsVisible()
    {
        // Arrange & Act — the mockup's "Redirect J. Tran?" title + CURRENT JOB (Silver / Hydraulic Pressure
        // Loss) → NEW JOB (Gold / Transmission Control Fault) swap cards.
        var cut = RenderDialog(Info());

        // Assert
        Assert.Contains("J. Tran", cut.Find("[data-testid='redirect-title']").TextContent);

        var current = cut.Find("[data-testid='redirect-current-job']");
        Assert.Contains("CURRENT JOB", current.TextContent);
        Assert.Contains("SILVER", current.TextContent);
        Assert.Contains("Hydraulic Pressure Loss", current.TextContent);

        var next = cut.Find("[data-testid='redirect-new-job']");
        Assert.Contains("NEW JOB", next.TextContent);
        Assert.Contains("GOLD", next.TextContent);
        Assert.Contains("Transmission Control Fault", next.TextContent);
    }

    [Fact]
    public void GivenRepInCooldown_WhenDialogRendered_ThenCooldownWarningVisible()
    {
        // Arrange & Act
        var cut = RenderDialog(Info(inCooldown: true));

        // Assert
        var warning = cut.Find("[data-testid='redirect-cooldown-warning']");
        Assert.Contains("cooldown", warning.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GivenRepNotInCooldown_WhenDialogRendered_ThenNoCooldownWarning()
    {
        // Arrange & Act
        var cut = RenderDialog(Info(inCooldown: false));

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='redirect-cooldown-warning']"));
    }

    [Fact]
    public void GivenGoldTierNewJobAndRepInCooldown_WhenDialogRendered_ThenGoldOverrideTextInWarning()
    {
        // Arrange & Act — the mockup's warning: "Gold tier overrides the cooldown."
        var cut = RenderDialog(Info(inCooldown: true, newTier: ServiceTier.Gold));

        // Assert
        var warning = cut.Find("[data-testid='redirect-cooldown-warning']");
        Assert.Contains("Gold tier overrides the cooldown", warning.TextContent);
    }

    [Fact]
    public void GivenIsRedirecting_WhenDialogRendered_ThenConfirmButtonDisabled()
    {
        // Arrange & Act — optimistic in-flight state (AC-3): the Confirm button is disabled.
        var cut = RenderDialog(Info(), isRedirecting: true);

        // Assert
        Assert.True(cut.Find("[data-testid='redirect-confirm']").HasAttribute("disabled"));
    }

    [Fact]
    public void GivenRedirectError_WhenDialogRendered_ThenErrorMessageVisible()
    {
        // Arrange & Act — AC-4 error surfaced in the dialog.
        var cut = RenderDialog(Info(), error: "Rep is no longer redirectable.");

        // Assert
        Assert.Contains(
            "Rep is no longer redirectable.", cut.Find("[data-testid='redirect-error']").TextContent);
    }

    [Fact]
    public async Task GivenDialog_WhenConfirmClicked_ThenOnConfirmFires()
    {
        // Arrange
        var confirmed = false;
        var cut = Render<RedirectConfirmDialog>(p => p
            .Add(c => c.ActiveRedirectInfo, Info())
            .Add(c => c.OnConfirm, () => confirmed = true));

        // Act
        await cut.Find("[data-testid='redirect-confirm']").ClickAsync(new());

        // Assert
        Assert.True(confirmed);
    }

    [Fact]
    public async Task GivenDialog_WhenCancelClicked_ThenOnCancelFires()
    {
        // Arrange
        var cancelled = false;
        var cut = Render<RedirectConfirmDialog>(p => p
            .Add(c => c.ActiveRedirectInfo, Info())
            .Add(c => c.OnCancel, () => cancelled = true));

        // Act
        await cut.Find("[data-testid='redirect-cancel']").ClickAsync(new());

        // Assert
        Assert.True(cancelled);
    }
}
