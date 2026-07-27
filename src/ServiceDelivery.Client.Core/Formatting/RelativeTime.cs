namespace ServiceDelivery.Client.Core.Formatting;

/// <summary>
/// Formats a request's age as the compact relative-time text the dispatcher queue card shows — "just now",
/// "4 min ago", "1 hr ago" (FE-004 AC-2, mockup: dispatcher-dashboard). Pure and clock-free: the caller
/// passes "now" so the result is deterministic and testable; the card supplies <c>DateTimeOffset.UtcNow</c>.
/// </summary>
public static class RelativeTime
{
    public static string Describe(DateTimeOffset createdAt, DateTimeOffset now)
    {
        var elapsed = now - createdAt;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes} min ago";
        }

        return $"{(int)elapsed.TotalHours} hr ago";
    }
}
