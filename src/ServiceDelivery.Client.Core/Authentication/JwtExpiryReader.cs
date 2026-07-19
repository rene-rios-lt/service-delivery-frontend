namespace ServiceDelivery.Client.Core.Authentication;

/// <summary>
/// Framework-free reader for a JWT's <c>exp</c> claim. Has no dependency on any JWT library,
/// HTTP, or DI — it parses the second (payload) segment of a compact JWS and compares the
/// <c>exp</c> claim (Unix seconds) to a supplied instant.
/// Fail-safe: any token that cannot be parsed, or that carries no <c>exp</c> claim, is
/// reported as expired so a bad token can never be treated as a live session.
/// </summary>
public static class JwtExpiryReader
{
    public static bool IsExpired(string? token, DateTimeOffset now)
    {
        var expiry = ReadExpiry(token);
        return expiry is null || expiry.Value <= now;
    }

    private static DateTimeOffset? ReadExpiry(string? token)
    {
        using var document = JwtPayloadReader.TryParsePayload(token);
        if (document is null)
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty("exp", out var expElement)
            || !expElement.TryGetInt64(out var expUnixSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds);
    }
}
