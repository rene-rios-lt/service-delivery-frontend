using Bunit;
using Microsoft.AspNetCore.Components;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.Dispatcher.Components;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// FE-022 bUnit coverage for <see cref="ForceReleaseConfirmDialog"/> (mockup:
/// dispatcher-force-release__desktop-1440x900). Asserts the four required fields render (rep name in the title,
/// vehicle registration, the request to be re-queued, the always-shown session-revoke warning — AC-2), the
/// error state (error banner visible, confirm disabled while releasing — AC-5), and that Cancel / Force-release
/// raise their EventCallbacks. Presentational only — all data comes from <see cref="ForceReleaseInfo"/>.
/// </summary>
public class ForceReleaseConfirmDialogTests : BunitContext
{
    private static ForceReleaseInfo Info(
        string repName = "R. Alvarez",
        string registration = "IOW-4471",
        string? requestTitle = "Hydraulic Pressure Loss") =>
        new(Guid.Parse("30000000-0000-0000-0000-000000000007"), repName, registration, requestTitle);

    private IRenderedComponent<ForceReleaseConfirmDialog> RenderDialog(
        ForceReleaseInfo info,
        bool isForceReleasing = false,
        string? forceReleaseError = null,
        EventCallback? onCancel = null,
        EventCallback? onConfirm = null) =>
        Render<ForceReleaseConfirmDialog>(p =>
        {
            p.Add(c => c.ActiveForceReleaseInfo, info);
            p.Add(c => c.IsForceReleasing, isForceReleasing);
            p.Add(c => c.ForceReleaseError, forceReleaseError);
            if (onCancel is not null)
            {
                p.Add(c => c.OnCancel, onCancel.Value);
            }

            if (onConfirm is not null)
            {
                p.Add(c => c.OnConfirm, onConfirm.Value);
            }
        });

    [Fact]
    public void GivenForceReleaseInfo_WhenDialogRendered_ThenRepNameIsVisible()
    {
        // Arrange
        var info = Info(repName: "R. Alvarez");

        // Act
        var cut = RenderDialog(info);

        // Assert — the mockup titles the dialog "Force-release R. Alvarez's vehicle?".
        Assert.Contains("R. Alvarez", cut.Find("[data-testid='force-release-title']").TextContent);
    }

    [Fact]
    public void GivenForceReleaseInfo_WhenDialogRendered_ThenVehicleRegistrationIsVisible()
    {
        // Arrange
        var info = Info(registration: "IOW-4471");

        // Act
        var cut = RenderDialog(info);

        // Assert
        Assert.Contains("IOW-4471", cut.Find("[data-testid='force-release-registration']").TextContent);
    }

    [Fact]
    public void GivenForceReleaseInfoWithRequestTitle_WhenDialogRendered_ThenRequestTitleIsVisible()
    {
        // Arrange
        var info = Info(requestTitle: "Hydraulic Pressure Loss");

        // Act
        var cut = RenderDialog(info);

        // Assert — the request that will be re-queued is shown so the dispatcher knows what stays Pending.
        Assert.Contains(
            "Hydraulic Pressure Loss",
            cut.Find("[data-testid='force-release-request-title']").TextContent);
    }

    [Fact]
    public void GivenForceReleaseInfoWithNoRequestTitle_WhenDialogRendered_ThenRequestTitleSectionIsAbsent()
    {
        // Arrange — a claimed but idle rep has no active request, so the request-title line is not rendered.
        var info = Info(requestTitle: null);

        // Act
        var cut = RenderDialog(info);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='force-release-request-title']"));
    }

    [Fact]
    public void GivenForceReleaseInfo_WhenDialogRendered_ThenSessionRevokeWarningIsVisible()
    {
        // Arrange
        var info = Info();

        // Act
        var cut = RenderDialog(info);

        // Assert — the amber warning is always shown (mockup): the rep's session is revoked on release.
        Assert.Contains(
            "session is revoked",
            cut.Find("[data-testid='force-release-session-warning']").TextContent);
    }

    [Fact]
    public void GivenANonNullForceReleaseError_WhenDialogRendered_ThenErrorBannerIsVisible()
    {
        // Arrange — AC-5: on a failed release the dialog stays open carrying the error banner.
        var info = Info();

        // Act
        var cut = RenderDialog(info, forceReleaseError: "Vehicle is no longer claimed.");

        // Assert
        Assert.Contains(
            "Vehicle is no longer claimed.",
            cut.Find("[data-testid='force-release-error']").TextContent);
    }

    [Fact]
    public void GivenNoForceReleaseError_WhenDialogRendered_ThenErrorBannerIsAbsent()
    {
        // Arrange — no error: the banner is not rendered.
        var info = Info();

        // Act
        var cut = RenderDialog(info, forceReleaseError: null);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='force-release-error']"));
    }

    [Fact]
    public void GivenIsForceReleasingTrue_WhenDialogRendered_ThenConfirmButtonIsDisabled()
    {
        // Arrange — AC-5: while the release POST is in flight the confirm button disables (prevents double-submit).
        var info = Info();

        // Act
        var cut = RenderDialog(info, isForceReleasing: true);

        // Assert
        Assert.True(cut.Find("[data-testid='force-release-confirm']").HasAttribute("disabled"));
    }

    [Fact]
    public void GivenIsForceReleasingFalse_WhenDialogRendered_ThenConfirmButtonIsEnabled()
    {
        // Arrange — idle dialog: the confirm button is actionable.
        var info = Info();

        // Act
        var cut = RenderDialog(info, isForceReleasing: false);

        // Assert
        Assert.False(cut.Find("[data-testid='force-release-confirm']").HasAttribute("disabled"));
    }

    [Fact]
    public void GivenADialog_WhenCancelClicked_ThenOnCancelIsRaised()
    {
        // Arrange
        var cancelled = false;
        var onCancel = EventCallback.Factory.Create(this, () => cancelled = true);
        var cut = RenderDialog(Info(), onCancel: onCancel);

        // Act
        cut.Find("[data-testid='force-release-cancel']").Click();

        // Assert
        Assert.True(cancelled);
    }

    [Fact]
    public void GivenADialog_WhenForceReleaseConfirmed_ThenOnConfirmIsRaised()
    {
        // Arrange
        var confirmed = false;
        var onConfirm = EventCallback.Factory.Create(this, () => confirmed = true);
        var cut = RenderDialog(Info(), onConfirm: onConfirm);

        // Act
        cut.Find("[data-testid='force-release-confirm']").Click();

        // Assert
        Assert.True(confirmed);
    }
}
