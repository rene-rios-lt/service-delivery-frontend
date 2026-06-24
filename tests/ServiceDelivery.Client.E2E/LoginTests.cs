namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-001 coverage: login routes by role with no role-selection step. Drives the live Web host as a
/// black box, asserting only on <c>data-testid</c> selectors and the resulting URL.
/// </summary>
[TestFixture]
public sealed class LoginTests : E2ETestBase
{
    [Test]
    public async Task GivenValidDispatcher1Credentials_WhenLoginSubmitted_ThenDispatcherDashboardIsShown()
    {
        // Arrange
        await Page.GotoAsync("/login");
        await Page.WaitForSelectorAsync("[data-testid='login-card']");

        // Act
        await Page.FillAsync("[data-testid='email-input'] input", "alex@dealer.com");
        await Page.FillAsync("[data-testid='password-input'] input", DispatcherPassword);
        await Page.ClickAsync("[data-testid='sign-in-button']");

        // Assert
        await Page.WaitForSelectorAsync("[data-testid='dispatcher-dashboard']");
        Assert.That(Page.Url, Does.Contain("/dispatcher"));
    }

    [Test]
    public async Task GivenInvalidCredentials_WhenLoginSubmitted_ThenInlineErrorIsShown()
    {
        // Arrange
        await Page.GotoAsync("/login");
        await Page.WaitForSelectorAsync("[data-testid='login-card']");

        // Act
        await Page.FillAsync("[data-testid='email-input'] input", "bad");
        await Page.FillAsync("[data-testid='password-input'] input", "wrong");
        await Page.ClickAsync("[data-testid='sign-in-button']");

        // Assert
        var error = await Page.WaitForSelectorAsync("[data-testid='login-error']");
        Assert.That(await error!.IsVisibleAsync(), Is.True);
        Assert.That(Page.Url, Does.Contain("/login"));
    }

    [Test]
    public async Task GivenValidDispatcher1Credentials_WhenLoginSubmitted_ThenNoRoleSelectionScreenAppears()
    {
        // Arrange
        await Page.GotoAsync("/login");
        await Page.WaitForSelectorAsync("[data-testid='login-card']");

        // Act
        await Page.FillAsync("[data-testid='email-input'] input", "alex@dealer.com");
        await Page.FillAsync("[data-testid='password-input'] input", DispatcherPassword);
        await Page.ClickAsync("[data-testid='sign-in-button']");

        // Assert
        await Page.WaitForSelectorAsync("[data-testid='dispatcher-dashboard']");
        Assert.That(Page.Url, Does.Contain("/dispatcher"));
        Assert.That(await Page.Locator("[data-testid='role-selection']").CountAsync(), Is.Zero);
        Assert.That(await Page.GetByText("Select role").CountAsync(), Is.Zero);
    }
}
