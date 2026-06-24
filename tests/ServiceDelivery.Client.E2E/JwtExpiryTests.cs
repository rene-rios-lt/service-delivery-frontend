namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-002 coverage: clearing the stored JWT (<c>localStorage["sd.auth.token"]</c>) forces a redirect
/// back to the login screen, and the cleared token is not reused on the next request. Drives the live
/// Web host as a black box.
/// </summary>
[TestFixture]
public sealed class JwtExpiryTests : E2ETestBase
{
    private const string TokenKey = "sd.auth.token";

    [Test]
    public async Task GivenStoredJwtIsCleared_WhenNextPageLoad_ThenRedirectedToLogin()
    {
        // Arrange
        await LoginAsDispatcherAsync();
        await Page.EvaluateAsync($"localStorage.removeItem('{TokenKey}')");

        // Act
        await Page.GotoAsync("/dispatcher");

        // Assert
        await Page.WaitForURLAsync("**/login");
        Assert.That(Page.Url, Does.Contain("/login"));
    }

    [Test]
    public async Task GivenStoredJwtIsCleared_WhenPageLoaded_ThenLoginPageDoesNotAutoRedirectAway()
    {
        // Arrange
        await LoginAsDispatcherAsync();
        await Page.EvaluateAsync($"localStorage.removeItem('{TokenKey}')");
        await Page.GotoAsync("/dispatcher");
        await Page.WaitForURLAsync("**/login");

        // Act
        await Page.WaitForTimeoutAsync(1000);

        // Assert
        Assert.That(Page.Url, Does.Contain("/login"));
        Assert.That(await Page.Locator("[data-testid='login-card']").IsVisibleAsync(), Is.True);
    }
}
