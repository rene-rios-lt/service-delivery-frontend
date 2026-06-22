using System;
using System.Text;
using System.Text.Json;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Tests.Authentication;

public class SessionStateTests
{
    private readonly Mock<ITokenStore> _tokenStore = new();

    private SessionState CreateSessionState() => new(_tokenStore.Object);

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string TokenWithExp(long expUnixSeconds)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { exp = expUnixSeconds })));
        return $"{header}.{payload}.signature";
    }

    [Fact]
    public async Task GivenAnExpiredStoredToken_WhenIsTokenExpiredCalled_ThenReturnsTrue()
    {
        // Arrange
        var token = TokenWithExp(DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds());
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(token);
        var sessionState = CreateSessionState();

        // Act
        var expired = await sessionState.IsTokenExpiredAsync();

        // Assert
        Assert.True(expired);
    }

    [Fact]
    public async Task GivenAValidStoredToken_WhenIsTokenExpiredCalled_ThenReturnsFalse()
    {
        // Arrange
        var token = TokenWithExp(DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds());
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync(token);
        var sessionState = CreateSessionState();

        // Act
        var expired = await sessionState.IsTokenExpiredAsync();

        // Assert
        Assert.False(expired);
    }

    [Fact]
    public async Task GivenNoStoredToken_WhenIsTokenExpiredCalled_ThenReturnsTrue()
    {
        // Arrange
        _tokenStore.Setup(t => t.GetTokenAsync()).ReturnsAsync((string?)null);
        var sessionState = CreateSessionState();

        // Act
        var expired = await sessionState.IsTokenExpiredAsync();

        // Assert
        Assert.True(expired);
    }
}
