using System.Text.Json;

namespace ServiceDelivery.Client.Core.Authentication;

/// <summary>
/// Shared, framework-free decode of a compact JWS payload segment, used by the per-claim readers
/// (<see cref="JwtExpiryReader"/>, <see cref="JwtRoleReader"/>) so the two never diverge in how they
/// locate, base64url-decode, and JSON-parse the payload. Each reader keeps its own claim-specific
/// logic; only the decode lives here.
/// </summary>
internal static class JwtPayloadReader
{
    /// <summary>
    /// Returns the parsed payload as a <see cref="JsonDocument"/> (the caller disposes it), or
    /// <c>null</c> for a token that is missing, has fewer than two segments, or whose payload
    /// segment cannot be base64url-decoded or JSON-parsed. Fail-safe: a bad token yields
    /// <c>null</c>, never an exception.
    /// </summary>
    public static JsonDocument? TryParsePayload(string? token)
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
            return JsonDocument.Parse(payloadJson);
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
