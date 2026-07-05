using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// Unit tests for <see cref="RequesterCompleteViewModel"/> (FE-019). The completion ViewModel reads the
/// assembled <see cref="ServiceCompletionData"/> from <see cref="IServiceCompletedStore"/> and exposes the
/// completion subtitle (AC-1) with graceful degrade when the store is unpopulated or either field is empty
/// (AC-4), plus the two navigation actions (AC-3), both returning the requester to their persona home. All
/// collaborators are mocked Core abstractions.
/// </summary>
public class RequesterCompleteViewModelTests
{
    private readonly Mock<IServiceCompletedStore> _store = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();

    private RequesterCompleteViewModel CreateViewModel(ServiceCompletionData? payload = null)
    {
        _store.SetupGet(s => s.CurrentPayload).Returns(payload);
        return new RequesterCompleteViewModel(_store.Object, _navigator.Object);
    }

    [Fact]
    public void GivenCurrentPayloadWithRepNameAndDtcTitle_WhenCompletionSubtitleAccessed_ThenSubtitleContainsBothFields()
    {
        // Arrange — AC-1/AC-4: with both the rep name and the DTC title present the subtitle is the full form
        // "{RepName} resolved your {DtcTitle}. Thanks for using Service Delivery." Distinct values per field
        // so neither can pass by coincidence (a dropped field would leave the other's value out).
        var viewModel = CreateViewModel(new ServiceCompletionData("Jordan Tran", "Transmission Control Fault"));

        // Act
        var subtitle = viewModel.CompletionSubtitle;

        // Assert
        Assert.Equal(
            "Jordan Tran resolved your Transmission Control Fault. Thanks for using Service Delivery.",
            subtitle);
    }

    [Fact]
    public void GivenNoCurrentPayloadInStore_WhenCompletionSubtitleAccessed_ThenSubtitleIsGenericForm()
    {
        // Arrange — AC-4 graceful degrade: on a refresh / deep-link the store is unpopulated (null payload),
        // so the subtitle degrades to the generic thank-you rather than rendering a half-built sentence.
        var viewModel = CreateViewModel(payload: null);

        // Act
        var subtitle = viewModel.CompletionSubtitle;

        // Assert
        Assert.Equal("Your service is complete. Thanks for using Service Delivery.", subtitle);
    }

    [Fact]
    public void GivenCurrentPayloadWithEmptyRepName_WhenCompletionSubtitleAccessed_ThenSubtitleIsGenericForm()
    {
        // Arrange — AC-4 graceful degrade: an absent rep name (empty) must not produce " resolved your …";
        // the subtitle degrades to the generic form.
        var viewModel = CreateViewModel(new ServiceCompletionData(string.Empty, "Transmission Control Fault"));

        // Act
        var subtitle = viewModel.CompletionSubtitle;

        // Assert
        Assert.Equal("Your service is complete. Thanks for using Service Delivery.", subtitle);
    }

    [Fact]
    public void GivenCurrentPayloadWithEmptyDtcTitle_WhenCompletionSubtitleAccessed_ThenSubtitleIsGenericForm()
    {
        // Arrange — AC-4 graceful degrade: an absent DTC title (empty) must not produce "resolved your .";
        // the subtitle degrades to the generic form.
        var viewModel = CreateViewModel(new ServiceCompletionData("Jordan Tran", string.Empty));

        // Act
        var subtitle = viewModel.CompletionSubtitle;

        // Assert
        Assert.Equal("Your service is complete. Thanks for using Service Delivery.", subtitle);
    }

    [Fact]
    public void GivenRequesterCompleteViewModel_WhenSubmitNewRequestCalled_ThenNavigatesToRequesterHome()
    {
        // Arrange — AC-3: the primary "Submit a new request" action returns the requester to their persona
        // home (the submit form).
        var viewModel = CreateViewModel();

        // Act
        viewModel.SubmitNewRequest();

        // Assert
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.Requester), Times.Once);
    }

    [Fact]
    public void GivenRequesterCompleteViewModel_WhenDismissCalled_ThenNavigatesToRequesterHome()
    {
        // Arrange — AC-3: the "Done" action also returns the requester to their persona home.
        var viewModel = CreateViewModel();

        // Act
        viewModel.Dismiss();

        // Assert
        _navigator.Verify(n => n.NavigateToPersonaHome(UserRole.Requester), Times.Once);
    }
}
