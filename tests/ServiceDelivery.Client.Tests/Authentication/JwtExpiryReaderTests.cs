using System;
using System.Text;
using System.Text.Json;
using ServiceDelivery.Client.Core.Authentication;

namespace ServiceDelivery.Client.Tests.Authentication;

public class JwtExpiryReaderTests
{
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
    public void GivenATokenWithPastExpClaim_WhenChecked_ThenReportsExpired()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var token = TokenWithExp(now.AddMinutes(-5).ToUnixTimeSeconds());

        // Act
        var expired = JwtExpiryReader.IsExpired(token, now);

        // Assert
        Assert.True(expired);
    }

    [Fact]
    public void GivenATokenWithFutureExpClaim_WhenChecked_ThenReportsNotExpired()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var token = TokenWithExp(now.AddMinutes(5).ToUnixTimeSeconds());

        // Act
        var expired = JwtExpiryReader.IsExpired(token, now);

        // Assert
        Assert.False(expired);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    [InlineData("only.two")]
    [InlineData("header.not-base64-!!!.signature")]
    [InlineData("header.eyJzdWIiOiJubyBleHAifQ.signature")]
    public void GivenAMalformedOrUnparseableToken_WhenChecked_ThenReportsExpired(string token)
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var expired = JwtExpiryReader.IsExpired(token, now);

        // Assert
        Assert.True(expired);
    }
}
