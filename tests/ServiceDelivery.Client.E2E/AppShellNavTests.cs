namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-021 coverage: an authenticated Dispatcher can reach the account menu from the app bar and log
/// out, returning to the login screen. The Web host uses the AccountMenu shell style — the avatar
/// opens an inline account panel. Drives the live Web host as a black box.
/// </summary>
[TestFixture]
public sealed class AppShellNavTests : E2ETestBase
{
    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenAvatarClicked_ThenAccountMenuPanelIsVisible()
    {
        // Arrange
        await LoginAsDispatcherAsync();

        // Act
        await Page.ClickAsync("[data-testid='persona-avatar']");

        // Assert
        var panel = await Page.WaitForSelectorAsync("[data-testid='persona-menu-account-panel']");
        Assert.That(await panel!.IsVisibleAsync(), Is.True);
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenLogoutClicked_ThenRedirectedToLoginScreen()
    {
        // Arrange
        await LoginAsDispatcherAsync();
        await Page.ClickAsync("[data-testid='persona-avatar']");
        await Page.WaitForSelectorAsync("[data-testid='persona-menu-account-panel']");

        // Act
        await Page.ClickAsync("[data-testid='menu-item-logout']");

        // Assert
        await Page.WaitForURLAsync("**/login");
        Assert.That(Page.Url, Does.Contain("/login"));
    }
}
