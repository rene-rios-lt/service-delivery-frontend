using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceDelivery.Client.E2E.Helpers;

/// <summary>
/// Test-setup utility for the Playwright AC-3 auto-transition scenario (FE-016) — the Web analogue of the
/// Appium <c>BackendApiHelper</c> that made the job-offer scenarios deterministic (BUG-032/040). The
/// requester pending view only transitions to <c>/requester/tracking</c> once the backend emits
/// <c>RepAssigned</c> over RequesterHub, which requires the matching algorithm to (a) match a positioned,
/// equipment-carrying vehicle in range, (b) offer it to a rep, and (c) have that rep ACCEPT. Relying on
/// the ambient simulator fleet happening to satisfy all three within the wait window is non-deterministic
/// (it timed out twice). This helper establishes the deterministic precondition against the live backend.
///
/// <para>
/// <b>Why the fleet must be positioned (the BUG-032 lesson).</b> The matching algorithm only considers a
/// rep whose claimed vehicle has a known position — seeded vehicles start with none, and the only thing
/// that ever sets one is the <c>Simulator</c>-role account POSTing to <c>/vehicles/{id}/position</c>. The
/// running simulator does move vehicles, but their positions drift across the Iowa loop, so a HydraulicTool
/// vehicle is not guaranteed to be in range of an arbitrary request site at the instant of submission.
/// Positioning the whole fleet at the request coordinates immediately before the UI submits guarantees an
/// in-range, equipment-matching candidate (V-001..V-007 all carry HydraulicTool — only V-008 does not), so
/// DTC-001 matches.
/// </para>
///
/// <para>
/// <b>Why a rep then accepts (the difference from Appium).</b> Unlike the Appium suite (which runs
/// backend-only with <c>SD_SKIP_SIMULATOR=1</c> and drives the accept through the app), the Playwright
/// suite runs the full system WITH the simulator operating <c>rep1..rep8</c>. Those reps auto-accept ~85%
/// of offers (decline ~15%) after a 1–5 s "reviewing" delay; on a decline the backend re-matches to the
/// next in-range candidate. With seven HydraulicTool vehicles positioned in range there is ample
/// redundancy, so an accept — and therefore the <c>RepAssigned</c> push — arrives well within the test's
/// widened wait. No manual accept is needed.
/// </para>
///
/// <para>
/// Uses only in-box <see cref="HttpClient"/> + <c>System.Net.Http.Json</c>; the E2E project treats the
/// system as a black box. Throws on any non-success HTTP status so a setup failure surfaces immediately
/// rather than being swallowed — the "did the transition happen" assertion stays in the test's
/// <c>WaitForURLAsync</c>.
/// </para>
/// </summary>
public static class BackendApiHelper
{
    /// <summary>Seeded <c>Simulator</c>-role account — the only role allowed to post vehicle positions.</summary>
    private const string SimulatorEmail = "simulator@system.internal";

    /// <summary>Shared default password for all seeded accounts.</summary>
    private const string SeedPassword = "Password123!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Positions every vehicle in the dealer fleet at the given coordinates, authenticating as the
    /// <c>Simulator</c> account (the only role permitted to POST positions). Call this immediately before
    /// the UI submits a DTC-001 request at the same coordinates so the matching algorithm finds an in-range
    /// HydraulicTool vehicle. Synchronous wrapper for use from NUnit test bodies. Throws
    /// <see cref="InvalidOperationException"/> if login, the fleet read, or any position POST fails.
    /// </summary>
    public static void PositionFleetAt(string baseUrl, double latitude, double longitude) =>
        PositionFleetAtAsync(baseUrl, latitude, longitude).GetAwaiter().GetResult();

    private static async Task PositionFleetAtAsync(string baseUrl, double latitude, double longitude)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, SimulatorEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var fleet = await client.GetFromJsonAsync<List<FleetEntry>>("/simulator/fleet-state", JsonOptions)
                    ?? new List<FleetEntry>();
        if (fleet.Count == 0)
        {
            throw new InvalidOperationException(
                "GET /simulator/fleet-state returned no vehicles — cannot position the fleet for matching.");
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

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var loginBody = new { email, password = SeedPassword };
        var response = await client.PostAsJsonAsync("/auth/login", loginBody);
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

    /// <summary>Shape of the <c>POST /auth/login</c> response (<c>{ "token": "..." }</c>).</summary>
    private sealed record LoginResponse(string Token);

    /// <summary>Minimal projection of a <c>GET /simulator/fleet-state</c> entry — only the id is needed.</summary>
    private sealed record FleetEntry(Guid VehicleId);
}
