using System.Text.Json;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Authentication;

/// <summary>
/// Framework-free reader for a JWT's <c>role</c> claim, the sibling of <see cref="JwtExpiryReader"/>.
/// Maps the <c>role</c> claim string to a <see cref="UserRole"/> (matching the backend, which writes
/// <c>new Claim("role", user.Role.ToString())</c>).
/// Fail-safe: any token that cannot be parsed, carries no <c>role</c> claim, or carries a value that
/// is not a known <see cref="UserRole"/> is reported as <c>null</c> so an unusable session can never
/// be treated as a routable persona.
/// </summary>
public static class JwtRoleReader
{
    public static UserRole? ReadRole(string? token)
    {
        using var document = JwtPayloadReader.TryParsePayload(token);
        if (document is null)
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty("role", out var roleElement)
            || roleElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var roleValue = roleElement.GetString();
        if (Enum.TryParse<UserRole>(roleValue, ignoreCase: false, out var role)
            && Enum.IsDefined(role))
        {
            return role;
        }

        return null;
    }
}
