namespace ServiceDelivery.Client.Appium;

/// <summary>
/// FE-001 coverage (AC-8): a ServiceRep logging in with <c>rep1</c> credentials is routed straight
/// to the take-over screen (not the idle view) with no role-selection step. Drives the installed
/// Mobile app on the iOS simulator as a black box, asserting on the XCUITest accessibility tree.
/// </summary>
[TestFixture]
public sealed class LoginTests : AppiumTestBase
{
    [Test]
    public void GivenRep1Credentials_WhenLoginSubmitted_ThenTakeOverScreenIsShown()
    {
        // Arrange
        var email = Driver.FindElement(MobileBy.AccessibilityId("email-input"));
        var password = Driver.FindElement(MobileBy.AccessibilityId("password-input"));

        // Act
        email.SendKeys("rep1");
        password.SendKeys(RepPassword);
        Driver.FindElement(MobileBy.AccessibilityId("sign-in-button")).Click();

        // Assert
        var takeOverButton = Driver.FindElement(MobileBy.AccessibilityId("take-over-button"));
        Assert.That(takeOverButton.Displayed, Is.True);
    }
}
