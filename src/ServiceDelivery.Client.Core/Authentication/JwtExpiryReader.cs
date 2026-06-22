using System.Text.Json;

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
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var segments = token.Split('.');
        if (segments.Length < 2)
        {
            return null;
        }

        try
        {
            var payloadJson = DecodeBase64Url(segments[1]);
            using var document = JsonDocument.Parse(payloadJson);

            if (!document.RootElement.TryGetProperty("exp", out var expElement)
                || !expElement.TryGetInt64(out var expUnixSeconds))
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string DecodeBase64Url(string segment)
    {
        var normalized = segment.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
        }

        var bytes = Convert.FromBase64String(normalized);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
