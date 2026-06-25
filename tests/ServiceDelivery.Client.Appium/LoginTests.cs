namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-001 coverage (AC-8): a ServiceRep logging in with <c>rep1</c> credentials is routed straight
/// to the take-over screen (not the idle view) with no role-selection step. Drives the installed
/// Mobile app on the iOS simulator as a black box, asserting on data-testid CSS selectors against
/// the WEBVIEW context (MAUI Blazor renders all content inside a WKWebView).
/// </summary>
[TestFixture]
public sealed class LoginTests : AppiumTestBase
{
    [Test]
    public void GivenRep1Credentials_WhenLoginSubmitted_ThenTakeOverScreenIsShown()
    {
        // Arrange / Act — FillInput types and commits the Blazor binding (change event); SendKeys
        // alone does not raise change, so the credentials would submit empty/partial otherwise.
        FillInput("email-input", "rep1@dealer.com");
        FillInput("password-input", RepPassword);
        Driver.FindElement(By.CssSelector("[data-testid='sign-in-button']")).Click();

        // Assert
        var takeOverButton = Driver.FindElement(By.CssSelector("[data-testid='take-over-button']"));
        Assert.That(takeOverButton.Displayed, Is.True);
    }
}
