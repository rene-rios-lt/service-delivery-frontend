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

    /// <summary>Seeded Dispatcher account — used to read the fleet (<c>GET /dispatcher/fleet</c>).</summary>
    private const string DispatcherEmail = "alex@dealer.com";

    /// <summary>Seeded <c>rep1</c> account — the rep the Appium suite takes over (see <c>TakeOverFirstIdleVehicle</c>).</summary>
    private const string Rep1Email = "rep1@dealer.com";

    /// <summary>Seeded <c>rep1</c> RepId (deterministic seed GUID) — used to find rep1's row in the dispatcher fleet.</summary>
    private static readonly Guid Rep1RepId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    /// <summary>Shared default password for all seeded accounts.</summary>
    private const string SeedPassword = "Password123!";

    /// <summary>DTC-001 — Hydraulic system fault (requires <c>HydraulicTool</c>), seeded GUID.</summary>
    private const string Dtc001Id = "20000000-0000-0000-0000-000000000001";

    /// <summary>Seeded V-001 (carries <c>HydraulicTool</c>) — the vehicle rep1 claims for the FE-019 flow.</summary>
    private const string Vehicle1Id = "30000000-0000-0000-0000-000000000001";

    /// <summary>
    /// Request site for the FE-019 mobile completion flow — the same fixed Des Moines-area point the
    /// simulator GPS is pinned to in the Appium test's [OneTimeSetUp], so the requester's submitted
    /// location, rep1's positioned vehicle, and the request all coincide (distance 0 → deterministic match).
    /// </summary>
    public const double CompletionRequestLatitude = 41.5868;
    public const double CompletionRequestLongitude = -93.6250;

    /// <summary>
    /// Where the fleet starts — the Iowa geographic centroid, permanently inside the simulator's
    /// operational area. The taken-over rep's vehicle gets this position so it is matchable; the request
    /// is then submitted ~100 mi east (see <see cref="RequestLongitude"/>) so the offer carries a real,
    /// non-zero distance and ETA instead of the degenerate 0 mi / 0 min a co-located request produced.
    /// </summary>
    private const double VehicleStartLatitude = 41.88;
    private const double VehicleStartLongitude = -93.10;

    /// <summary>
    /// Request location — same latitude as the fleet start but ~100 mi due east (still eastern Iowa,
    /// inside the operational area). At this latitude one degree of longitude is about 51.5 mi, so the
    /// +1.944 deg offset is about 100 mi: the backend's Haversine distance is therefore about 100 mi and,
    /// at the assumed 60 mph, the ETA is about 100 min — real values the offer screen can display.
    /// Determinism is unchanged: with the rep-operating simulator disabled the taken-over rep is still
    /// the sole match candidate (there is no max-match-radius), so distance only affects the displayed
    /// numbers, not which rep is matched.
    /// </summary>
    private const double RequestLatitude = 41.88;
    private const double RequestLongitude = -91.156;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Positions the fleet at the start point then submits one Gold-tier service request about 100 mi
    /// east (DTC-001), so the offer carries a real distance and ETA. Positioning (as the <c>Simulator</c>
    /// account) is what makes the taken-over rep visible to
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
        PositionFleetAtAsync(baseUrl, RequestLatitude, RequestLongitude).GetAwaiter().GetResult();
    }

    private static async Task SubmitServiceRequestAsync(string baseUrl)
    {
        // 1. Give every vehicle a position at the FLEET START point. Matching ignores vehicles with no
        //    position, and in a backend-only run nothing else posts one. Positioning at the start (not
        //    the request site) leaves the rep about 100 mi from the request, so the offer's distance/ETA
        //    are real; the rep is still the sole candidate, so the match is unaffected.
        await PositionFleetAtAsync(baseUrl, VehicleStartLatitude, VehicleStartLongitude);

        // 2. Submit the matching request as the Gold-tier requester.
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, GoldRequesterEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            dtcId = Dtc001Id,
            latitude = RequestLatitude,
            longitude = RequestLongitude
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
    /// Authenticates as the <c>Simulator</c> account, reads the dealer fleet, and posts the given
    /// position for every vehicle. Used two ways: at the fleet-start point to make the taken-over rep
    /// matchable (about 100 mi from the request), and again at the request site after the rep accepts to
    /// drive the <c>Within15Miles</c> transition (distance 0). Mirrors the real simulator, which posts
    /// positions for all vehicles. Throws if the fleet is empty or any call returns a non-success status.
    /// </summary>
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
                "GET /simulator/fleet-state returned no vehicles — cannot position the fleet for matching.");
        }

        foreach (var vehicle in fleet)
        {
            var body = new
            {
                latitude,
                longitude,
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

    /// <summary>
    /// FE-019 step 1 (before the requester app submits): makes rep1 a matchable, Available candidate AT the
    /// request site. rep1 claims V-001 (HydraulicTool) — a benign 409 means it already holds a vehicle — and
    /// the whole fleet is positioned at the request coordinates (as the <c>Simulator</c> account) so rep1 is
    /// distance 0 from the request. With the rep-operating simulator disabled (backend-only Appium run) rep1
    /// is then the sole match candidate, so the requester's single submission routes an offer to it
    /// deterministically. Throws on any login / claim-auth / position failure.
    /// </summary>
    public static void PrepareRep1AtRequestSite(string baseUrl) =>
        PrepareRep1AtRequestSiteAsync(baseUrl).GetAwaiter().GetResult();

    private static async Task PrepareRep1AtRequestSiteAsync(string baseUrl)
    {
        using var repClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var repToken = await LoginAsync(repClient, Rep1Email);
        repClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);

        // A 409 means rep1 already holds a vehicle (from an earlier scenario) — the desired end state, so it
        // is not an error. Any other failure surfaces on the downstream match/offer wait rather than here.
        await repClient.PostAsync($"/vehicles/{Vehicle1Id}/claim", content: null);

        await PositionFleetAtAsync(baseUrl, CompletionRequestLatitude, CompletionRequestLongitude);
    }

    /// <summary>
    /// FE-019 step 2 (after the requester app has submitted and reached the pending screen): drives rep1's
    /// offer through to completion so the backend fires <c>ServiceCompleted</c> to the requester's group and
    /// the mobile app auto-navigates to <c>/requester/complete</c>. Polls <c>GET /job-offers/pending</c> for
    /// rep1's pushed offer, then <c>POST /job-offers/{id}/accept</c> → re-post a distance-0 position at the
    /// request site + wait for the <c>EnRoute→Within15Miles</c> transition → <c>POST /rep/arrive</c> →
    /// <c>POST /rep/complete</c>. Throws on any step failure (or if no offer appears / the rep never reaches
    /// Within15Miles within the poll budget) so a broken precondition surfaces immediately rather than as a
    /// downstream UI timeout.
    /// </summary>
    public static void DriveRep1ToCompletion(string baseUrl) =>
        DriveRep1ToCompletionAsync(baseUrl).GetAwaiter().GetResult();

    private static async Task DriveRep1ToCompletionAsync(string baseUrl)
    {
        using var repClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var repToken = await LoginAsync(repClient, Rep1Email);
        repClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);

        var offerId = await WaitForPendingOfferAsync(repClient);

        var accept = await repClient.PostAsync($"/job-offers/{offerId}/accept", content: null);
        await EnsureSuccessAsync(accept, $"POST /job-offers/{offerId}/accept");

        // After accept rep1 is EnRoute, but /rep/arrive requires Within15Miles — the backend state machine is
        // EnRoute → Within15Miles → OnSite. The EnRoute→Within15Miles transition is driven ONLY by a position
        // POST received WHILE the rep is on an active job (UpdateVehiclePositionCommandHandler recomputes
        // proximity). In the Playwright run the simulator's position stream does this; the Appium harness runs
        // backend-only (SD_SKIP_SIMULATOR=1), so the harness must post it. Re-post the whole fleet at the
        // FE-019 request site — CompletionRequestLatitude/Longitude, where PrepareRep1AtRequestSite positioned
        // the fleet and the requester's GPS submit landed, so rep1's distance to ITS active request is 0
        // (< 15 mi). NOTE: this deliberately does NOT call PositionFleetAtRequestSite, which targets the
        // separate JobOffer/ActiveJob scenario's coordinates (~100 mi east) — posting there would leave rep1
        // far from the FE-019 request and it would never reach Within15Miles.
        await PositionFleetAtAsync(baseUrl, CompletionRequestLatitude, CompletionRequestLongitude);

        // The proximity recompute is async, so bound-poll until rep1 has actually reached Within15Miles
        // before arriving (rather than firing /rep/arrive into the race and 400ing on EnRoute).
        await WaitForRep1Within15MilesAsync(baseUrl);

        var arrive = await repClient.PostAsync("/rep/arrive", content: null);
        await EnsureSuccessAsync(arrive, "POST /rep/arrive");

        var complete = await repClient.PostAsync("/rep/complete", content: null);
        await EnsureSuccessAsync(complete, "POST /rep/complete");
    }

    /// <summary>
    /// Bounded poll (mirrors <see cref="WaitForPendingOfferAsync"/>) that waits until rep1 has reached the
    /// <c>Within15Miles</c> state after a distance-0 position re-post, reading the state from
    /// <c>GET /dispatcher/fleet</c>. <c>/rep/arrive</c> is only valid from <c>Within15Miles</c>, so this
    /// closes the async-recompute race deterministically. Throws with a clear message if rep1 has not
    /// transitioned within the budget so a genuine regression fails loudly rather than as an arrive 400.
    /// </summary>
    private static async Task WaitForRep1Within15MilesAsync(string baseUrl)
    {
        const int maxAttempts = 20; // 20 × 500 ms = 10 s.
        const int pollDelayMs = 500;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var state = await GetRep1FleetStateAsync(baseUrl);
            if (state is not null && state.State == "Within15Miles")
            {
                return;
            }

            await Task.Delay(pollDelayMs);
        }

        var finalState = await GetRep1FleetStateAsync(baseUrl);
        throw new InvalidOperationException(
            "rep1 did not reach Within15Miles within 10s after re-posting a distance-0 position at the " +
            $"request site (current state: {finalState?.State ?? "unknown"}) — /rep/arrive requires " +
            "Within15Miles, so the job cannot be driven to completion.");
    }

    /// <summary>
    /// Polls <c>GET /job-offers/pending</c> (which 404s until an offer is pushed) until rep1's offer appears
    /// or the bounded budget elapses. Returns the offer id to accept. Throws if none appears in time.
    /// </summary>
    private static async Task<Guid> WaitForPendingOfferAsync(HttpClient repClient)
    {
        const int maxAttempts = 60; // 60 × 500 ms = 30 s — covers match + SignalR/DB settle.
        const int pollDelayMs = 500;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await repClient.GetAsync("/job-offers/pending");
            if (response.IsSuccessStatusCode)
            {
                var offer = await response.Content.ReadFromJsonAsync<PendingOffer>(JsonOptions);
                if (offer is not null && offer.OfferId != Guid.Empty)
                {
                    return offer.OfferId;
                }
            }

            await Task.Delay(pollDelayMs);
        }

        throw new InvalidOperationException(
            "No pending job offer appeared for rep1 within 30s — the requester's request was never matched " +
            "and offered, so the job cannot be driven to completion.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"{operation} failed ({(int)response.StatusCode} {response.StatusCode}): {content}");
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

    /// <summary>
    /// QUAL-009: reads rep1's current row from <c>GET /dispatcher/fleet</c> so a heartbeat / go-off-duty
    /// scenario can assert backend-side state that has no UI surface (the heartbeat runs in the
    /// background of the rep views with no on-screen indicator). Returns null if rep1 is not present in
    /// the fleet. Throws on any login or fleet-read failure — never swallows an error.
    /// </summary>
    public static RepFleetState? GetRep1FleetState(string baseUrl)
    {
        return GetRep1FleetStateAsync(baseUrl).GetAwaiter().GetResult();
    }

    /// <summary>
    /// QUAL-009: true when vehicle <paramref name="vehicleId"/> appears in <c>GET /vehicles/available</c>
    /// (the take-over dropdown). After a human-takeover rep is timed out and its vehicle is parked, the
    /// vehicle reappears here for a fresh take-over — this lets a scenario assert that reappearance.
    /// </summary>
    public static bool IsVehicleTakeable(string baseUrl, Guid vehicleId)
    {
        return IsVehicleTakeableAsync(baseUrl, vehicleId).GetAwaiter().GetResult();
    }

    /// <summary>
    /// QUAL-009: polls until rep1 has been swept off duty (state <c>Offline</c>, <c>HumanControlled</c>
    /// cleared) AND its vehicle has reappeared in the take-over list, or the timeout elapses. Returns
    /// true if both held within <paramref name="timeout"/>. Used by the heartbeat-timeout scenario after
    /// the app is closed so heartbeats stop — the backend's stale-heartbeat sweep should then fire.
    /// </summary>
    public static bool WaitUntilOffDutyAndTakeable(string baseUrl, Guid vehicleId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var state = GetRep1FleetState(baseUrl);
            if (state is { State: "Offline", HumanControlled: false } && IsVehicleTakeable(baseUrl, vehicleId))
            {
                return true;
            }
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
        return false;
    }

    private static async Task<RepFleetState?> GetRep1FleetStateAsync(string baseUrl)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, DispatcherEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var fleet = await client.GetFromJsonAsync<List<DispatcherFleetEntry>>("/dispatcher/fleet", JsonOptions)
                    ?? new List<DispatcherFleetEntry>();
        var row = fleet.FirstOrDefault(e => e.RepId == Rep1RepId);
        return row is null ? null : new RepFleetState(row.State, row.HumanControlled, row.VehicleId);
    }

    private static async Task<bool> IsVehicleTakeableAsync(string baseUrl, Guid vehicleId)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, Rep1Email);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var available = await client.GetFromJsonAsync<List<AvailableVehicle>>("/vehicles/available", JsonOptions)
                        ?? new List<AvailableVehicle>();
        return available.Any(v => v.VehicleId == vehicleId);
    }

    /// <summary>Rep state observed via the dispatcher fleet, for QUAL-009 backend-side assertions.</summary>
    public sealed record RepFleetState(string State, bool HumanControlled, Guid VehicleId);

    /// <summary>Shape of the <c>POST /auth/login</c> response (<c>{ "token": "..." }</c>).</summary>
    private sealed record LoginResponse(string Token);

    /// <summary>Minimal projection of a <c>GET /simulator/fleet-state</c> entry — only the id is needed.</summary>
    private sealed record FleetEntry(Guid VehicleId);

    /// <summary>Minimal projection of the <c>GET /job-offers/pending</c> response — only the offer id is needed.</summary>
    private sealed record PendingOffer(Guid OfferId);

    /// <summary>Minimal projection of a <c>GET /dispatcher/fleet</c> entry for QUAL-009 assertions.</summary>
    private sealed record DispatcherFleetEntry(Guid RepId, string State, Guid VehicleId, bool HumanControlled);

    /// <summary>Minimal projection of a <c>GET /vehicles/available</c> entry — only the id is needed.</summary>
    private sealed record AvailableVehicle(Guid VehicleId);
}
