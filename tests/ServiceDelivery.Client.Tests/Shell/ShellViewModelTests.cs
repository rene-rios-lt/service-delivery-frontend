using System.Collections.Generic;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Shell;

public class ShellViewModelTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();
    private readonly Mock<ILogoutSideEffect> _sideEffect = new();
    private readonly Mock<IReleaseVehicleAction> _releaseAction = new();
    private readonly Mock<IShellPresentation> _presentation = new();

    private ShellViewModel CreateViewModel()
    {
        return new ShellViewModel(
            _tokenStore.Object,
            _navigator.Object,
            _sideEffect.Object,
            _releaseAction.Object,
            _presentation.Object,
            new PersonaMenuFactory());
    }

    [Fact]
    public void GivenANewShell_WhenTitleRead_ThenReturnsTheDefaultTitle()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        Assert.Equal("Service Delivery", vm.Title);
    }

    [Fact]
    public void GivenATitleOverride_WhenTitleRead_ThenReturnsTheOverride()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SetTitle("Incoming Job Offer");

        // Assert
        Assert.Equal("Incoming Job Offer", vm.Title);
    }

    [Fact]
    public void GivenATitleOverride_WhenClearedWithNull_ThenReturnsTheDefaultTitle()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SetTitle("Incoming Job Offer");

        // Act
        vm.SetTitle(null);

        // Assert
        Assert.Equal("Service Delivery", vm.Title);
    }

    [Fact]
    public void GivenANewShell_WhenSubtitleRead_ThenReturnsNull()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        Assert.Null(vm.Subtitle);
    }

    [Fact]
    public void GivenASubtitleOverride_WhenSubtitleRead_ThenReturnsTheOverride()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SetSubtitle("Navigating to requester");

        // Assert
        Assert.Equal("Navigating to requester", vm.Subtitle);
    }

    [Fact]
    public void GivenASubtitleOverride_WhenClearedWithNull_ThenSubtitleReturnsNull()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SetSubtitle("Navigating to requester");

        // Act
        vm.SetSubtitle(null);

        // Assert
        Assert.Null(vm.Subtitle);
    }

    [Fact]
    public void GivenANewShell_WhenIsMenuAffordanceVisibleRead_ThenItIsTrueByDefault()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        Assert.True(vm.IsMenuAffordanceVisible);
    }

    [Fact]
    public void GivenTheMenuAffordanceHidden_WhenIsMenuAffordanceVisibleRead_ThenItIsFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SetMenuAffordanceVisible(false);

        // Assert
        Assert.False(vm.IsMenuAffordanceVisible);
    }

    [Fact]
    public async Task GivenAnAuthenticatedSession_WhenLogoutAsyncCalled_ThenTokenStoreIsCleared()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.LogoutAsync();

        // Assert
        _tokenStore.Verify(t => t.ClearAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenAnAuthenticatedSession_WhenLogoutAsyncCalled_ThenNavigatesToLogin()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.LogoutAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToLogin(), Times.Once);
    }

    [Fact]
    public async Task GivenLogout_WhenSequenced_ThenSideEffectRunsBeforeTokenIsCleared()
    {
        // Arrange
        var sequence = new List<string>();
        _sideEffect.Setup(s => s.RunBeforeTokenClearedAsync())
            .Callback(() => sequence.Add("side-effect"))
            .Returns(Task.CompletedTask);
        _tokenStore.Setup(t => t.ClearAsync())
            .Callback(() => sequence.Add("clear"))
            .Returns(Task.CompletedTask);
        var vm = CreateViewModel();

        // Act
        await vm.LogoutAsync();

        // Assert
        Assert.Equal(new[] { "side-effect", "clear" }, sequence.ToArray());
    }

    [Fact]
    public async Task GivenAReleaseAction_WhenReleaseVehicleInvoked_ThenActionSeamIsCalled()
    {
        // Arrange
        _releaseAction.Setup(r => r.ReleaseAsync())
            .ReturnsAsync(ReleaseVehicleResult.NothingToRelease);
        var vm = CreateViewModel();

        // Act
        await vm.ReleaseVehicleAsync();

        // Assert
        _releaseAction.Verify(r => r.ReleaseAsync(), Times.Once);
    }

    [Fact]
    public void GivenShellViewModel_WhenMenuStyleRead_ThenReflectsInjectedPresentation()
    {
        // Arrange
        _presentation.SetupGet(p => p.MenuStyle).Returns(ShellMenuStyle.Drawer);
        var vm = CreateViewModel();

        // Act
        var style = vm.MenuStyle;

        // Assert
        Assert.Equal(ShellMenuStyle.Drawer, style);
    }

    [Fact]
    public void GivenAProfile_WhenLoaded_ThenMenuModelIsBuiltForThatPersona()
    {
        // Arrange
        var profile = new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid());
        var vm = CreateViewModel();

        // Act
        vm.Load(profile);

        // Assert
        Assert.NotNull(vm.Menu);
        Assert.Equal("Rosa Alvarez", vm.Menu!.Title);
    }

    [Fact]
    public void GivenALoadedShell_WhenSetVehicleContextCalled_ThenMenuVehicleContextIsUpdated()
    {
        // Arrange
        var profile = new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid());
        var vm = CreateViewModel();
        vm.Load(profile);

        // Act
        vm.SetVehicleContext("Vehicle IA-4471 · On shift");

        // Assert
        Assert.Equal("Vehicle IA-4471 · On shift", vm.Menu!.VehicleContext);
    }

    [Fact]
    public void GivenALoadedShell_WhenSetReleaseEnabledFalse_ThenReleaseMenuItemIsDisabled()
    {
        // Arrange
        // FE-014/AC-2: the InProgress screen is the production caller that gates the release item.
        // It calls SetReleaseEnabled(false) on the already-loaded shell — no profile re-load needed.
        var profile = new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid());
        var vm = CreateViewModel();
        vm.Load(profile);

        // Act
        vm.SetReleaseEnabled(false);

        // Assert
        var releaseItem = vm.Menu!.Items.Single(i => i.ActionKey == PersonaMenuFactory.ReleaseActionKey);
        Assert.False(releaseItem.IsEnabled);
    }

    [Fact]
    public void GivenAReleaseGatedShell_WhenSetReleaseEnabledTrue_ThenReleaseMenuItemIsReEnabled()
    {
        // Arrange
        // The InProgress screen re-enables the item on leave (Dispose): idle/complete → releasable again.
        var profile = new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid());
        var vm = CreateViewModel();
        vm.Load(profile);
        vm.SetReleaseEnabled(false);

        // Act
        vm.SetReleaseEnabled(true);

        // Assert
        var releaseItem = vm.Menu!.Items.Single(i => i.ActionKey == PersonaMenuFactory.ReleaseActionKey);
        Assert.True(releaseItem.IsEnabled);
    }

    [Fact]
    public void GivenNoMenuLoaded_WhenSetReleaseEnabledCalled_ThenItIsANoOp()
    {
        // Arrange
        // Guard: a route may gate before the menu exists; this must not throw (mirrors SetVehicleContext).
        var vm = CreateViewModel();

        // Act
        vm.SetReleaseEnabled(false);

        // Assert
        Assert.Null(vm.Menu);
    }

    [Fact]
    public void GivenAnOpenMenu_WhenToggleMenuCalled_ThenMenuStateFlips()
    {
        // Arrange
        var vm = CreateViewModel();
        var initial = vm.IsMenuOpen;

        // Act
        vm.ToggleMenu();

        // Assert
        Assert.NotEqual(initial, vm.IsMenuOpen);
    }
}
