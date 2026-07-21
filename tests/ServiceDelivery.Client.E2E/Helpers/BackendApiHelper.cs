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

    /// <summary>
    /// Seeded spare rep accounts + their distinct seeded HydraulicTool vehicles (V-002/V-003). The redirect
    /// scenario needs at least one Available rep OTHER than the one being redirected, so a distinct rep can
    /// re-accept the displaced request. The live simulator does not reliably supply that second rep on its
    /// own: <c>GET /vehicles/available</c> does not exclude already-claimed vehicles, so every simulator rep
    /// races to claim V-001 (the first entry) and all but one get a 409 — leaving exactly one Available rep
    /// on a clean start. We work around that by claiming distinct vehicles for these spare reps directly
    /// (each claim names its own vehicle, so no collision). The simulator's decision loops are connected for
    /// all rep1..rep8, so once a spare rep is Available and in range it auto-accepts (AutoDeclineRatePercent=0).
    /// </summary>
    private static readonly (string Email, string VehicleId)[] SpareReps =
    {
        ("rep2@dealer.com", "30000000-0000-0000-0000-000000000002"),
        ("rep3@dealer.com", "30000000-0000-0000-0000-000000000003"),
    };

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

    /// <summary>
    /// Seeded rep accounts keyed by their deterministic seed GUID (SeedConstants.Rep1Id..Rep8Id =
    /// 50000000-…-000N). The assignment picks whichever in-range rep the simulator operates, so to drive that
    /// rep's job to completion (FE-019) we must map its ClaimingRepId back to its login email.
    /// </summary>
    private static readonly IReadOnlyDictionary<Guid, string> RepAccountsById = new Dictionary<Guid, string>
    {
        [Guid.Parse("50000000-0000-0000-0000-000000000001")] = "rep1@dealer.com",
        [Guid.Parse("50000000-0000-0000-0000-000000000002")] = "rep2@dealer.com",
        [Guid.Parse("50000000-0000-0000-0000-000000000003")] = "rep3@dealer.com",
        [Guid.Parse("50000000-0000-0000-0000-000000000004")] = "rep4@dealer.com",
        [Guid.Parse("50000000-0000-0000-0000-000000000005")] = "rep5@dealer.com",
        [Guid.Parse("50000000-0000-0000-0000-000000000006")] = "rep6@dealer.com",
        [Guid.Parse("50000000-0000-0000-0000-000000000007")] = "rep7@dealer.com",
        [Guid.Parse("50000000-0000-0000-0000-000000000008")] = "rep8@dealer.com",
    };

    /// <summary>
    /// Drives the assigned rep's job through to completion against the live backend so the tracked requester
    /// (already on the tracking page) receives the <c>ServiceCompleted</c> push (FE-019). Synchronous wrapper
    /// for NUnit test bodies. Throws <see cref="InvalidOperationException"/> on any step failure so the cause
    /// surfaces immediately rather than being swallowed by a later UI wait.
    ///
    /// <para>
    /// The completion flow the backend enforces (RepController + Complete/ArriveCommandHandler):
    /// <list type="number">
    /// <item>Find the EnRoute rep whose active request is at the tracked coordinates via the Simulator-role
    /// <c>GET /simulator/fleet-state</c> — the rep serving THIS request (not any EnRoute rep on the shared
    /// fleet), so the ServiceCompleted lands on the tracked requester.</item>
    /// <item>Map that rep's <c>ClaimingRepId</c> back to its seeded login email (the reps are simulator-driven,
    /// so we authenticate AS the rep to POST its own state transitions).</item>
    /// <item>As that rep: <c>POST /rep/arrive</c> (EnRoute → OnSite, request → InProgress) then
    /// <c>POST /rep/complete</c> (OnSite → Available, request → Completed). The complete handler fires
    /// <c>ServiceCompleted</c> to <c>requester:{requesterId}</c> — exactly the event the tracking page now
    /// handles by navigating to <c>/requester/complete</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static void CompleteAssignedRequestAt(string baseUrl, double latitude, double longitude) =>
        CompleteAssignedRequestAtAsync(baseUrl, latitude, longitude).GetAwaiter().GetResult();

    private static async Task CompleteAssignedRequestAtAsync(string baseUrl, double latitude, double longitude)
    {
        using var simClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var simToken = await LoginAsync(simClient, SimulatorEmail);
        simClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", simToken);

        // Find the rep serving the tracked request (EnRoute, or already Within15Miles — the simulator drives the
        // truck in and the backend auto-advances the proximity state, so we must accept both), then keep its
        // vehicle at the request site so no proximity edge case can interfere before it marks arrival.
        var assigned = await WaitForRepServingRequestAtAsync(simClient, latitude, longitude, "EnRoute", "Within15Miles");
        await PostPositionAsync(simClient, assigned.VehicleId, latitude, longitude);

        var repId = assigned.ClaimingRepId!.Value;
        if (!RepAccountsById.TryGetValue(repId, out var repEmail))
        {
            throw new InvalidOperationException(
                $"No seeded rep account is known for rep id {repId} — cannot drive the job to completion. " +
                "The assignment must be to one of the seeded rep1..rep8 accounts.");
        }

        using var repClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var repToken = await LoginAsync(repClient, repEmail);
        repClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", repToken);

        var arrive = await repClient.PostAsync("/rep/arrive", content: null);
        await EnsureSuccessAsync(arrive, "POST /rep/arrive");

        var complete = await repClient.PostAsync("/rep/complete", content: null);
        await EnsureSuccessAsync(complete, "POST /rep/complete");
    }

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

    /// <summary>
    /// Drives a full, real redirect against the live backend so the displaced requester (already on the
    /// tracking page) receives the two redirect events — <c>RepAssigned</c> then <c>RepRedirected</c>
    /// (FE-018). Synchronous wrapper for NUnit test bodies. Throws <see cref="InvalidOperationException"/> on
    /// any setup step failure so the cause surfaces immediately rather than being swallowed by a later UI wait.
    ///
    /// <para>
    /// The real redirect flow the backend enforces (business-rules.md / RedirectRepCommandHandler):
    /// <list type="number">
    /// <item>Find the EnRoute rep + vehicle currently assigned to the tracked (displaced) request via the
    /// Simulator-role <c>GET /simulator/fleet-state</c>.</item>
    /// <item>Move that vehicle FAR from the tracked requester (> 15 mi) so the redirect passes the backend's
    /// <c>WithinFifteenMiles</c> proximity guard — a rep within 15 mi of its requester cannot be redirected.
    /// This is the one place the tracking precondition (fleet positioned AT the requester) must be undone.</item>
    /// <item>Submit a GOLD target request at the far coordinates as a second requester so the target tier is
    /// strictly higher (or Gold), clearing the <c>TierNotHigher</c> guard.</item>
    /// <item>As the dispatcher, POST <c>/dispatcher/redirect { repId, toRequestId }</c>. The backend returns
    /// the displaced request to Pending, marks it <c>DisplacedFromRepId</c>, and re-runs matching on it.</item>
    /// <item>The simulator's reps then accept the re-matched displaced request; on that accept the backend
    /// fires <c>RepAssigned</c> (new rep's full picture) followed by the deferred <c>RepRedirected</c> to the
    /// displaced requester — exactly the two events the tracking page now handles.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Not deterministic on the new rep's identity.</b> Which rep re-accepts the displaced job is up to the
    /// live fleet, so the caller asserts only that the banner appears and the rep name/vehicle changed — not a
    /// specific new name (mirroring the RequesterTrackingTests determinism note).
    /// </para>
    /// </summary>
    public static void TriggerRedirect(
        string baseUrl,
        double trackedLatitude,
        double trackedLongitude,
        double farLatitude,
        double farLongitude,
        string dispatcherEmail,
        string goldRequesterEmail,
        string goldDtcId) =>
        TriggerRedirectAsync(
                baseUrl, trackedLatitude, trackedLongitude, farLatitude, farLongitude,
                dispatcherEmail, goldRequesterEmail, goldDtcId)
            .GetAwaiter().GetResult();

    private static async Task TriggerRedirectAsync(
        string baseUrl,
        double trackedLatitude,
        double trackedLongitude,
        double farLatitude,
        double farLongitude,
        string dispatcherEmail,
        string goldRequesterEmail,
        string goldDtcId)
    {
        using var simClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var simToken = await LoginAsync(simClient, SimulatorEmail);
        simClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", simToken);

        // 1. Guarantee a SECOND, distinct Available rep exists to re-accept the displaced request. On a clean
        //    live start the simulator claims only ONE vehicle (the /vehicles/available bug — it lists claimed
        //    vehicles, so every rep races for V-001 and all but one 409), which is enough for a single
        //    assignment but not for a redirect, where the redirected rep leaves and a DIFFERENT rep must take
        //    the displaced job. Claiming distinct vehicles for these spare reps directly sidesteps the race.
        await EnsureSpareRepsClaimedAsync(baseUrl);

        // 2. Find the EnRoute rep assigned to OUR tracked request (its vehicle carries the assignment).
        //    The suite runs multiple requester scenarios against a shared live fleet, so more than one rep can
        //    be EnRoute at once (e.g. RequesterFindingTests leaves its gold1 rep driving). Picking any EnRoute
        //    rep (the old FirstOrDefault) could redirect the WRONG rep, sending RepAssigned + RepRedirected to
        //    a different requester and never to the tracked one. fleet-state exposes each rep's
        //    ActiveRequestLocation, so we select the rep whose active request is at the tracked coordinates.
        //    EnRoute only: POST /dispatcher/redirect is an EnRoute-only action (a Within15Miles rep is
        //    proximity-locked), so a broader accept set here would grab a rep the redirect would then reject.
        var assigned = await WaitForRepServingRequestAtAsync(simClient, trackedLatitude, trackedLongitude, "EnRoute");

        // 3. Move ONLY the assigned vehicle far from the tracked requester so the redirect clears the 15-mi
        //    proximity guard, then re-assert every OTHER vehicle at the tracked coordinates. The displaced
        //    request is matched exactly ONCE — synchronously inside the redirect handler (step 5) — and nothing
        //    re-runs matching on a later position update, so a distinct in-range Available rep MUST already be
        //    positioned at the tracked site at the instant of the redirect. Positioning the spares in range
        //    only AFTER the redirect (the old step-5-only approach) missed that single matching window and the
        //    displaced request stayed Pending forever.
        await PostPositionAsync(simClient, assigned.VehicleId, farLatitude, farLongitude);
        await PositionAllExceptAsync(simClient, assigned.VehicleId, trackedLatitude, trackedLongitude);

        // 4. Submit a GOLD target request at the far coordinates (strictly-higher/Gold tier clears the guard).
        using var goldClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var goldToken = await LoginAsync(goldClient, goldRequesterEmail);
        goldClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", goldToken);
        var target = await goldClient.PostAsJsonAsync("/service-requests", new
        {
            dtcId = Guid.Parse(goldDtcId),
            latitude = farLatitude,
            longitude = farLongitude
        });
        await EnsureSuccessAsync(target, "POST /service-requests (Gold target)");
        var targetBody = await target.Content.ReadFromJsonAsync<SubmitResponse>(JsonOptions)
            ?? throw new InvalidOperationException("POST /service-requests returned no body for the Gold target.");

        // 5. As the dispatcher, redirect the EnRoute rep to the Gold target — this displaces the tracked
        //    request and immediately re-runs matching, which now finds an in-range Available spare rep (step 3).
        using var dispatcherClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var dispatcherToken = await LoginAsync(dispatcherClient, dispatcherEmail);
        dispatcherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", dispatcherToken);
        var redirect = await dispatcherClient.PostAsJsonAsync("/dispatcher/redirect", new
        {
            repId = assigned.ClaimingRepId!.Value,
            toRequestId = targetBody.RequestId
        });
        await EnsureSuccessAsync(redirect, "POST /dispatcher/redirect");

        // 6. Keep every other vehicle at the tracked site so, should the immediate match miss (timing) and a
        //    later rep transition to Available re-trigger matching, an in-range candidate is still present.
        await PositionAllExceptAsync(simClient, assigned.VehicleId, trackedLatitude, trackedLongitude);
    }

    /// <summary>
    /// Claims a distinct HydraulicTool vehicle for each spare rep so at least one Available rep exists BESIDES
    /// the one that will be redirected. Each claim names its own vehicle (no race), so a rep already holding a
    /// vehicle simply returns a benign 409 that is ignored; only a genuine failure (e.g. auth) would surface
    /// via <see cref="LoginAsync"/>. Idempotent and safe to call once per redirect scenario.
    ///
    /// <para>
    /// <b>Why this keeps hand-picking V-002/V-003 (BUG-045).</b> The production fix for the take-over collision
    /// lives in <c>TakeOverViewModel.TakeOverAsync</c>, which now auto-retries the next available candidate on a
    /// 409 (see the ViewModel unit tests — the primary guard for the retry pattern). This helper is deliberately
    /// NOT switched to that retry path: it targets the separate <c>POST /vehicles/{id}/claim</c> endpoint (not
    /// the production <c>/take-over</c> path), and naming a distinct vehicle per spare rep is collision-free by
    /// construction — no 409 race to retry through. Duplicating the ViewModel's retry loop here in a raw HTTP
    /// client would add timing surface for zero benefit, so the explicit-GUID approach is intentional
    /// test-harness behaviour, not the bug being fixed.
    /// </para>
    /// </summary>
    private static async Task EnsureSpareRepsClaimedAsync(string baseUrl)
    {
        foreach (var (email, vehicleId) in SpareReps)
        {
            using var repClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var token = await LoginAsync(repClient, email);
            repClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // A 409 means this rep already holds a vehicle (from an earlier scenario or a prior tick) — that is
            // exactly the desired end state, so it is not an error. Any other non-success is left to surface on
            // the downstream assignment wait rather than masking a real problem here.
            await repClient.PostAsync($"/vehicles/{vehicleId}/claim", content: null);
        }
    }

    /// <summary>
    /// Positions every vehicle EXCEPT the excluded one at the given coordinates. Used to keep the spare reps in
    /// range at the tracked site while the redirected vehicle stays far away (so it is not re-matched to the
    /// displaced request it was just redirected off).
    /// </summary>
    private static async Task PositionAllExceptAsync(
        HttpClient simClient, Guid excludeVehicleId, double latitude, double longitude)
    {
        var fleet = await simClient.GetFromJsonAsync<List<FleetEntry>>("/simulator/fleet-state", JsonOptions)
                    ?? new List<FleetEntry>();
        foreach (var vehicle in fleet.Where(v => v.VehicleId != excludeVehicleId))
            await PostPositionAsync(simClient, vehicle.VehicleId, latitude, longitude);
    }

    /// <summary>
    /// Coordinates that identify the same request site are considered equal within this tolerance. The
    /// active-request location echoed by fleet-state is the request's own submitted lat/lng, so an exact
    /// double match is expected in practice; the small epsilon only absorbs round-trip formatting noise.
    /// </summary>
    private const double CoordinateMatchToleranceDegrees = 0.0005; // ~55 m — far tighter than the 15-mi guard.

    /// <summary>
    /// Polls fleet-state (bounded) until a rep in one of <paramref name="acceptStates"/> whose ACTIVE REQUEST
    /// is at the tracked coordinates appears — i.e. the rep this test's own request was assigned to. Selecting
    /// by active-request location (not "any servicing rep") guarantees the caller acts on the rep serving the
    /// tracked request, so its SignalR events land on the tracked requester's page. The poll absorbs the
    /// match → offer → accept-delay chain and the shared-fleet timing (another test's rep may already be
    /// servicing); it never relies on a single snapshot.
    ///
    /// <para>
    /// <b>Why the accepted states differ by caller.</b> The redirect path passes <c>EnRoute</c> only —
    /// <c>POST /dispatcher/redirect</c> is an EnRoute-only action (a <c>Within15Miles</c> rep is proximity-locked,
    /// see business-rules.md). The completion path passes <c>EnRoute</c> AND <c>Within15Miles</c>: once a rep
    /// accepts, the simulator drives its truck toward the requester and the backend auto-transitions
    /// EnRoute → Within15Miles on each position update, so the rep can (and did — a live flake) race past the
    /// fleeting EnRoute state before this 500 ms poll catches it. Both are "actively servicing" states from
    /// which the subsequent pin-position + <c>/rep/arrive</c> (which itself requires Within15Miles) succeeds.
    /// </para>
    ///
    /// Throws with a fleet dump if no matching rep appears within the bound so a genuine assignment failure
    /// surfaces immediately instead of as a downstream UI timeout.
    /// </summary>
    private static async Task<FleetEntry> WaitForRepServingRequestAtAsync(
        HttpClient simClient, double latitude, double longitude, params string[] acceptStates)
    {
        const int maxAttempts = 60;      // 60 × 500 ms = 30 s — comfortably covers match + 1–5 s accept + delivery.
        const int pollDelayMs = 500;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var fleet = await simClient.GetFromJsonAsync<List<FleetEntry>>("/simulator/fleet-state", JsonOptions)
                        ?? new List<FleetEntry>();

            var match = fleet.FirstOrDefault(v =>
                v.ClaimingRepId is not null &&
                acceptStates.Contains(v.RepState) &&
                v.ActiveRequestLocation is not null &&
                Math.Abs(v.ActiveRequestLocation.Lat - latitude) <= CoordinateMatchToleranceDegrees &&
                Math.Abs(v.ActiveRequestLocation.Lng - longitude) <= CoordinateMatchToleranceDegrees);

            if (match is not null)
                return match;

            await Task.Delay(pollDelayMs);
        }

        var finalFleet = await simClient.GetFromJsonAsync<List<FleetEntry>>("/simulator/fleet-state", JsonOptions)
                         ?? new List<FleetEntry>();
        var dump = string.Join("; ", finalFleet.Select(v =>
            $"veh={v.VehicleId} rep={v.ClaimingRepId} state={v.RepState} " +
            $"activeReq={(v.ActiveRequestLocation is null ? "none" : $"{v.ActiveRequestLocation.Lat},{v.ActiveRequestLocation.Lng}")}"));
        throw new InvalidOperationException(
            $"No rep in state [{string.Join("/", acceptStates)}] whose active request is at ({latitude},{longitude}) " +
            $"appeared within {maxAttempts * pollDelayMs / 1000}s — the tracked request was never assigned to a " +
            $"servicing rep at those coordinates. Fleet: [{dump}]");
    }

    private static async Task PostPositionAsync(HttpClient client, Guid vehicleId, double latitude, double longitude)
    {
        var body = new { latitude, longitude, timestamp = DateTime.UtcNow };
        var response = await client.PostAsJsonAsync($"/vehicles/{vehicleId}/position", body);
        await EnsureSuccessAsync(response, $"POST /vehicles/{vehicleId}/position");
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

    /// <summary>Shape of the <c>POST /service-requests</c> response (<c>{ "requestId": "...", "status": "..." }</c>).</summary>
    private sealed record SubmitResponse(Guid RequestId, string Status);

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

    /// <summary>
    /// Projection of a <c>GET /simulator/fleet-state</c> entry. <c>VehicleId</c> is used to POST positions;
    /// <c>ClaimingRepId</c> + <c>RepState</c> identify an EnRoute rep, and <c>ActiveRequestLocation</c> pins
    /// that rep to a specific request site so the redirect targets the rep serving the TRACKED request rather
    /// than any EnRoute rep on the shared fleet (FE-018).
    /// </summary>
    private sealed record FleetEntry(
        Guid VehicleId,
        Guid? ClaimingRepId,
        string RepState,
        ActiveRequestLocationEntry? ActiveRequestLocation);

    /// <summary>Projection of a fleet-state entry's <c>activeRequestLocation</c> (<c>{ "lat": …, "lng": … }</c>).</summary>
    private sealed record ActiveRequestLocationEntry(double Lat, double Lng);
}
