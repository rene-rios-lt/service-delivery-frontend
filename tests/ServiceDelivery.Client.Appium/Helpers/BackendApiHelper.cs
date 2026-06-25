using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceDelivery.Client.Appium.Helpers;

/// <summary>
/// Test-setup utility for the Appium job-offer / active-job scenarios (BUG-032). The Appium suite
/// drives the Mobile app as a black box, but the job-offer screen only appears once the backend's
/// matching algorithm has a service request to dispatch <b>and</b> at least one eligible rep to
/// dispatch it to. This helper establishes both halves of that precondition against the live
/// backend: it positions the fleet (as the seeded <c>Simulator</c> account) and then submits one
/// service request (as the seeded <b>Gold-tier requester</b>, <c>gold1@example.com</c>) so an offer
/// is pushed to the taken-over rep.
///
/// <para>
/// <b>Why the fleet must be positioned (the BUG-032 cycle-1/2 failure).</b> The matching algorithm
/// only considers a rep whose claimed vehicle has a known position — <c>GetAvailableByDealerAsync</c>
/// inner-joins on <c>Vehicle.LastLatitude/LastLongitude != null</c>. Seeded vehicles start with
/// <b>no</b> position; the only thing that ever sets one is the <c>Simulator</c> account POSTing to
/// <c>/vehicles/{id}/position</c>. The Appium suite runs backend-only (see below), so nothing posts
/// positions and every vehicle is invisible to matching — a submitted request finds zero candidates,
/// goes <c>Pending</c>, and no offer is ever pushed. Earlier revisions submitted a request without
/// positioning the fleet and timed out for exactly this reason. This helper therefore posts a
/// position for every vehicle the <c>Simulator</c> can see before submitting the request, mirroring
/// what the real simulator does in a normal run.
/// </para>
///
/// <para>
/// <b>Backend-only environment.</b> The Appium suite runs with the rep-operating simulator disabled —
/// <c>scripts/local/test-appium.sh</c> sets <c>SD_SKIP_SIMULATOR=1</c>, which
/// <c>scripts/local/start.sh</c> honours by bringing up the backend without the simulator that
/// operates rep1..rep8. A human take-over (driven by the test through the app) is therefore the only
/// thing that makes a rep <c>Available</c>, so the taken-over Appium rep is the <b>sole</b> match
/// candidate. Positioning every vehicle at the request site gives that one Available rep distance 0;
/// with no rep competition there is no retry — a single submitted request routes its offer to that
/// rep deterministically.
/// </para>
///
/// <para>
/// <b>Routing.</b> Positions and the request all use the geographic centroid of Iowa (lat 41.88,
/// lng -93.10) with DTC-001 (<c>HydraulicTool</c> required). The taken-over rep claims the first idle
/// vehicle (V-001..V-007 all carry <c>HydraulicTool</c>; only V-008 does not), so the request matches
/// the rep under test. Submitting as the Gold tier gives the request top matching priority.
/// </para>
///
/// <para>
/// The helper uses only in-box <see cref="HttpClient"/> + <c>System.Net.Http.Json</c>; the Appium
/// project has no src/ project references and treats the app as a black box. It throws on any
/// non-success HTTP status so a submission failure surfaces immediately rather than being swallowed;
/// the "did the offer reach the UI" assertion stays in each test's existing <c>WaitForSignalR</c>
/// poll, so a missing offer still fails loudly there.
/// </para>
/// </summary>
public static class BackendApiHelper
{
    /// <summary>Seeded Gold-tier requester email (highest matching priority).</summary>
    private const string GoldRequesterEmail = "gold1@example.com";

    /// <summary>Seeded <c>Simulator</c>-role account — the only role allowed to post vehicle positions.</summary>
    private const string SimulatorEmail = "simulator@system.internal";

    /// <summary>Shared default password for all seeded accounts.</summary>
    private const string SeedPassword = "Password123!";

    /// <summary>DTC-001 — Hydraulic system fault (requires <c>HydraulicTool</c>), seeded GUID.</summary>
    private const string Dtc001Id = "20000000-0000-0000-0000-000000000001";

    /// <summary>Iowa geographic centroid — permanently inside the simulator's operational area.</summary>
    private const double IowaCentroidLatitude = 41.88;
    private const double IowaCentroidLongitude = -93.10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Positions the fleet then submits one Gold-tier service request at the Iowa centroid for DTC-001.
    /// Positioning (as the <c>Simulator</c> account) is what makes the taken-over rep visible to
    /// matching; with the rep-operating simulator disabled (backend-only run) that rep is then the sole
    /// match candidate, so the single submission routes an offer to it deterministically. Throws
    /// <see cref="InvalidOperationException"/> if any login, position, or submission returns a
    /// non-success status — errors are never swallowed. The "offer reached the UI" assertion remains in
    /// the caller's <c>WaitForSignalR</c> poll.
    /// </summary>
    public static void SubmitServiceRequest(string baseUrl)
    {
        SubmitServiceRequestAsync(baseUrl).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Re-posts a position at the request site for the whole fleet, as the <c>Simulator</c> account.
    /// Call this <b>after</b> the rep has accepted the offer (the rep is now <c>EnRoute</c>): the
    /// <c>Within15Miles</c> transition is driven only by <c>UpdateVehiclePositionCommandHandler</c>,
    /// which recomputes proximity on a position POST received <i>while the rep is on an active job</i>.
    /// The positioning inside <see cref="SubmitServiceRequest"/> happens <i>before</i> the rep accepts,
    /// so it only makes the rep matchable. Posting the site position again here (distance 0 &lt; 15 mi)
    /// transitions the rep to <c>Within15Miles</c>, which the active-job poll then surfaces to enable the
    /// "I've Arrived" button. Throws if the fleet is empty or any call returns a non-success status.
    /// </summary>
    public static void PositionFleetAtRequestSite(string baseUrl)
    {
        PositionFleetAtRequestSiteAsync(baseUrl).GetAwaiter().GetResult();
    }

    private static async Task SubmitServiceRequestAsync(string baseUrl)
    {
        // 1. Give every vehicle a position at the request site. Matching ignores vehicles with no
        //    position, and in a backend-only run nothing else posts one. Without this the taken-over
        //    rep is invisible to matching and no offer is ever generated.
        await PositionFleetAtRequestSiteAsync(baseUrl);

        // 2. Submit the matching request as the Gold-tier requester.
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, GoldRequesterEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            dtcId = Dtc001Id,
            latitude = IowaCentroidLatitude,
            longitude = IowaCentroidLongitude
        };

        var response = await client.PostAsJsonAsync("/service-requests", body);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"POST /service-requests failed ({(int)response.StatusCode} {response.StatusCode}): {content}");
        }
    }

    /// <summary>
    /// Authenticates as the <c>Simulator</c> account, reads the dealer fleet, and posts a position at
    /// the Iowa centroid for every vehicle — so whichever vehicle the test took over has a location and
    /// becomes an eligible match candidate. Mirrors the real simulator, which posts positions for all
    /// vehicles. Throws if the fleet is empty or any call returns a non-success status.
    /// </summary>
    private static async Task PositionFleetAtRequestSiteAsync(string baseUrl)
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
                "GET /simulator/fleet-state returned no vehicles — cannot position the fleet for matching.");
        }

        foreach (var vehicle in fleet)
        {
            var body = new
            {
                latitude = IowaCentroidLatitude,
                longitude = IowaCentroidLongitude,
                timestamp = DateTime.UtcNow
            };

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
        var loginBody = new
        {
            email,
            password = SeedPassword
        };

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
