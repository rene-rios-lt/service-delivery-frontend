namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Maps a rep/vehicle state string to the canonical design-system hex colour token (FE-024 AC-3).
/// Single source of truth for marker colours, consumed by the <c>GoogleMap</c> component and any future
/// map consumer (FE-026, FE-027, FE-003). The hex values are the authoritative tokens bundled into the
/// scoped CSS (RepIdle.razor.css / ActiveJob.razor.css); an unrecognised state falls back to offline grey.
/// </summary>
public static class RepStateColour
{
    // Offline grey doubles as the fallback for any unrecognised state — a rep with no known live state is
    // shown as offline rather than as an unstyled/empty marker (AC-3 fallback).
    public const string OfflineGrey = "#9AA0AE";

    public static string ForState(string state) => state switch
    {
        "Available" => "#2E9E5B",
        "EnRoute" => "#1E88E5",
        "Within15Miles" => "#F4A100",
        "OnSite" => "#E5392F",
        _ => OfflineGrey,
    };
}
