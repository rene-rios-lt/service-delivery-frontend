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
    public void GivenALoadedShell_WhenSetFocusedModeCalled_ThenTitleOverrideAndSuppressFlagAreSet()
    {
        // Arrange
        // AC-4: the job-offer screen is a focused accept/decline decision — the app bar overrides its
        // title and hides the menu affordance + avatar while the offer is on screen.
        var profile = new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid());
        var vm = CreateViewModel();
        vm.Load(profile);

        // Act
        vm.SetFocusedMode("Incoming Job Offer", "Vehicle IA-4471");

        // Assert
        Assert.Equal("Incoming Job Offer", vm.TitleOverride);
        Assert.True(vm.SuppressMenuAffordance);
    }

    [Fact]
    public void GivenALoadedShell_WhenSetFocusedModeCalled_ThenSubtitleIsPassedToVehicleContext()
    {
        // Arrange
        var profile = new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid());
        var vm = CreateViewModel();
        vm.Load(profile);

        // Act
        vm.SetFocusedMode("Incoming Job Offer", "Vehicle IA-4471");

        // Assert
        Assert.Equal("Vehicle IA-4471", vm.Menu!.VehicleContext);
    }

    [Fact]
    public void GivenShellInFocusedMode_WhenClearFocusedModeCalled_ThenTitleOverrideIsNullAndSuppressFlagIsFalse()
    {
        // Arrange
        // Navigating away from the offer screen (accept, decline, or expiry) restores normal chrome.
        var profile = new UserProfile(Guid.NewGuid(), "Rosa Alvarez", UserRole.ServiceRep, ServiceTier.None, Guid.NewGuid());
        var vm = CreateViewModel();
        vm.Load(profile);
        vm.SetFocusedMode("Incoming Job Offer", "Vehicle IA-4471");

        // Act
        vm.ClearFocusedMode();

        // Assert
        Assert.Null(vm.TitleOverride);
        Assert.False(vm.SuppressMenuAffordance);
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
