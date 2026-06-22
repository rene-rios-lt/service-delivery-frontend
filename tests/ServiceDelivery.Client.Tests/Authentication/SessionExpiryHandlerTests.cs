using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Tests.Authentication;

public class SessionExpiryHandlerTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();
    private readonly Mock<IPersonaNavigator> _navigator = new();

    private SessionExpiryHandler CreateHandler() => new(_tokenStore.Object, _navigator.Object);

    [Fact]
    public async Task GivenAnExpiredSession_WhenHandled_ThenStoredTokenIsCleared()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        await handler.HandleExpiredSessionAsync();

        // Assert
        _tokenStore.Verify(t => t.ClearAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenAnExpiredSession_WhenHandled_ThenNavigatesToLogin()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        await handler.HandleExpiredSessionAsync();

        // Assert
        _navigator.Verify(n => n.NavigateToLogin(), Times.Once);
    }

    [Fact]
    public async Task GivenAnExpiredSession_WhenHandled_ThenTokenIsClearedBeforeRedirect()
    {
        // Arrange
        var calls = new List<string>();
        _tokenStore.Setup(t => t.ClearAsync())
            .Callback(() => calls.Add("clear"))
            .Returns(Task.CompletedTask);
        _navigator.Setup(n => n.NavigateToLogin())
            .Callback(() => calls.Add("navigate"));
        var handler = CreateHandler();

        // Act
        await handler.HandleExpiredSessionAsync();

        // Assert
        Assert.Equal(new[] { "clear", "navigate" }, calls);
    }
}
