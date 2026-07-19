using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceDelivery.Client.Appium.Mac.Helpers;

/// <summary>
/// Test-setup utility for the Desktop Mac2Driver fleet-map scenarios (FE-003 Phase 3). The suite runs
/// backend-only (test-appium-mac.sh sets <c>SD_SKIP_SIMULATOR=1</c> via start.sh), so nothing posts vehicle
/// positions on its own — and the dispatcher fleet map only shows markers for vehicles that have a live
/// position and a non-Offline state. This helper posts a position for every vehicle (as the seeded
/// <c>Simulator</c> account, the only role allowed to), which is what makes the fleet's vehicles visible on
/// the dispatcher map so the render assertions have something to see. A small trimmed copy of the iOS
/// Appium project's helper — cross-project sharing via NuGet is not worth the overhead for a POC.
/// </summary>
public static class BackendApiHelper
{
    /// <summary>Seeded <c>Simulator</c>-role account — the only role allowed to post vehicle positions.</summary>
    private const string SimulatorEmail = "simulator@system.internal";

    /// <summary>Seeded Dispatcher account — used to read the fleet (<c>GET /dispatcher/fleet</c>).</summary>
    private const string DispatcherEmail = "alex@dealer.com";

    /// <summary>Seeded <c>rep1</c> account — the rep this suite takes over so a vehicle becomes non-Offline.</summary>
    private const string Rep1Email = "rep1@dealer.com";

    /// <summary>Seeded V-001 (carries <c>HydraulicTool</c>) — the vehicle rep1 claims, mirroring the iOS suite.</summary>
    private const string Vehicle1Id = "30000000-0000-0000-0000-000000000001";

    /// <summary>Shared default password for all seeded accounts.</summary>
    private const string SeedPassword = "Password123!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Posts the given position for every vehicle the <c>Simulator</c> account can see, so the dispatcher
    /// fleet has positioned vehicles to render as markers. Throws if the fleet is empty or any call returns
    /// a non-success status — a broken precondition surfaces immediately rather than as a downstream UI
    /// timeout.
    /// </summary>
    public static void PositionFleetAt(string baseUrl, double latitude, double longitude) =>
        PositionFleetAtAsync(baseUrl, latitude, longitude).GetAwaiter().GetResult();

    /// <summary>
    /// Claims V-001 as the seeded <c>rep1</c> — the takeover that flips the vehicle's rep-state from
    /// <c>Offline</c> to a visible (<c>Available</c>) state. This is the arrange for the SNAPSHOT render path:
    /// once V-001 is claimed AND positioned (see <see cref="PositionFleetAt"/>), <c>GET /dispatcher/fleet</c>
    /// returns it with a non-Offline state and a last position, so the dispatcher map renders its marker
    /// straight from <c>LoadAsync</c> — no live hub delivery required. Mirrors the iOS suite's claim pattern
    /// (login as rep1 → <c>POST /vehicles/{Vehicle1Id}/claim</c> with null content).
    /// <para>
    /// A non-success claim is tolerated ONLY when it is a <see cref="System.Net.HttpStatusCode.Conflict"/>,
    /// which means the vehicle is already claimed. In this backend-only run (<c>SD_SKIP_SIMULATOR=1</c>, no
    /// simulator reps logged in) rep1 is the SOLE actor that ever claims a vehicle, so an already-claimed
    /// V-001 can only be rep1 re-claiming it on a warm / reused backend — the desired end state, not an error.
    /// Any other non-success status is fatal and throws so a broken precondition surfaces immediately rather
    /// than as a downstream "no marker rendered" timeout.
    /// </para>
    /// </summary>
    public static void ClaimVehicleAsRep(string baseUrl) =>
        ClaimVehicleAsRepAsync(baseUrl).GetAwaiter().GetResult();

    /// <summary>
    /// Reads the number of dispatcher-visible fleet entries (rep-state known and not "Offline") from
    /// <c>GET /dispatcher/fleet</c>, so a scenario can assert backend-side fleet visibility independent of
    /// the map render. Throws on any login or fleet-read failure.
    /// </summary>
    public static int GetVisibleFleetCount(string baseUrl) =>
        GetVisibleFleetCountAsync(baseUrl).GetAwaiter().GetResult();

    private static async Task PositionFleetAtAsync(string baseUrl, double latitude, double longitude)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, SimulatorEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var fleet = await client.GetFromJsonAsync<List<FleetEntry>>("/simulator/fleet-state", JsonOptions)
                    ?? new List<FleetEntry>();
        if (fleet.Count == 0)
        {
            throw new InvalidOperationException(
                "GET /simulator/fleet-state returned no vehicles — cannot position the fleet for the map.");
        }

        foreach (var vehicle in fleet)
        {
            var body = new { latitude, longitude, timestamp = DateTime.UtcNow };
            var response = await client.PostAsJsonAsync($"/vehicles/{vehicle.VehicleId}/position", body);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"POST /vehicles/{vehicle.VehicleId}/position failed " +
                    $"({(int)response.StatusCode} {response.StatusCode}): {content}");
            }
        }
    }

    private static async Task ClaimVehicleAsRepAsync(string baseUrl)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, Rep1Email);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/vehicles/{Vehicle1Id}/claim", content: null);

        // A 409 Conflict means V-001 is already claimed. Backend-only (SD_SKIP_SIMULATOR=1) has no other rep
        // logged in and this helper is the only claimer, so an already-claimed V-001 can only be rep1 on a
        // warm/reused backend — the desired end state, tolerated. Any other failure is fatal.
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"POST /vehicles/{Vehicle1Id}/claim failed ({(int)response.StatusCode} {response.StatusCode}): {body}");
    }

    private static async Task<int> GetVisibleFleetCountAsync(string baseUrl)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, DispatcherEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var fleet = await client.GetFromJsonAsync<List<DispatcherFleetEntry>>("/dispatcher/fleet", JsonOptions)
                    ?? new List<DispatcherFleetEntry>();
        return fleet.Count(e => !string.IsNullOrEmpty(e.State) && e.State != "Offline");
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = SeedPassword });
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"POST /auth/login failed for {email} ({(int)response.StatusCode} {response.StatusCode}): {content}");
        }

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        if (result is null || string.IsNullOrWhiteSpace(result.Token))
        {
            throw new InvalidOperationException($"POST /auth/login returned no token for {email}.");
        }

        return result.Token;
    }

    private sealed record LoginResponse(string Token);

    private sealed record FleetEntry(Guid VehicleId);

    private sealed record DispatcherFleetEntry(Guid VehicleId, string State);
}
