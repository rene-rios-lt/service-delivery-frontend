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
    /// Submits a service request as the given seeded requester at the given coordinates and returns the new
    /// request id, authenticating via <c>POST /auth/login</c> then <c>POST /service-requests</c>. This is the
    /// arrange for the FE-004 Desktop queue-presence tests: in the backend-only Mac2 run
    /// (<c>SD_SKIP_SIMULATOR=1</c>) no rep is online, so the submitted request stays Pending and appears as an
    /// active card on the dispatcher queue. Synchronous wrapper for NUnit test bodies; throws
    /// <see cref="InvalidOperationException"/> on any login or submit failure.
    /// </summary>
    public static Guid SubmitServiceRequest(
        string baseUrl, string requesterEmail, string dtcId, double latitude, double longitude) =>
        SubmitServiceRequestAsync(baseUrl, requesterEmail, dtcId, latitude, longitude).GetAwaiter().GetResult();

    public static async Task<Guid> SubmitServiceRequestAsync(
        string baseUrl, string requesterEmail, string dtcId, double latitude, double longitude)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, requesterEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/service-requests", new
        {
            dtcId = Guid.Parse(dtcId),
            latitude,
            longitude
        });
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"POST /service-requests failed ({(int)response.StatusCode} {response.StatusCode}): {content}");
        }

        var body = await response.Content.ReadFromJsonAsync<SubmitResponse>(JsonOptions)
            ?? throw new InvalidOperationException("POST /service-requests returned no body.");
        return body.RequestId;
    }

    private sealed record SubmitResponse(Guid RequestId, string Status);

    /// <summary>
    /// Drives a submitted request all the way to <b>Assigned</b> against the backend-only Mac run
    /// (<c>SD_SKIP_SIMULATOR=1</c>, so no rep-operating simulator exists to accept offers on its own). This is
    /// the Desktop analogue of the Playwright AC-3 "matched-and-assigned" real-time-add path: claim a dedicated
    /// vehicle for the given rep, position it AT the request coordinates (so that rep is the nearest qualified
    /// candidate — there is no matching-radius cap), submit the request as the requester, then accept the offer
    /// AS that rep. On accept the backend emits <c>ServiceRequestAssigned</c> to <c>dealer:{id}</c>, which the
    /// dispatcher rail adds as a card in real time (the upsert path). Returns the new request id. Synchronous
    /// wrapper for NUnit test bodies; throws <see cref="InvalidOperationException"/> on any step failure so a
    /// broken precondition surfaces immediately rather than as a downstream UI timeout.
    /// </summary>
    /// <summary>
    /// Claims a vehicle as its rep and positions it — the pre-login precondition that puts the rep into the
    /// dispatcher's <c>GET /dispatcher/fleet</c> SNAPSHOT with a known <c>RepId</c> and a visible (non-Offline)
    /// state. This mirrors the real system, where the simulator claims all vehicles at startup BEFORE any
    /// dispatcher logs in, so a dispatcher's snapshot always carries rep identities; only a rep's active-request
    /// tier changes live during the session (delivered by the FE-005 real-time overlay). Without this, a
    /// clean-backend vehicle is unclaimed at snapshot time and the position-update merge preserves its snapshot
    /// <c>RepId=null</c> (it updates only lat/lng/state), so redirect eligibility — keyed by rep — can never match.
    /// </summary>
    public static void ClaimAndPositionVehicle(
        string baseUrl, string repEmail, string vehicleId, double latitude, double longitude) =>
        ClaimAndPositionVehicleAsync(baseUrl, repEmail, vehicleId, latitude, longitude).GetAwaiter().GetResult();

    private static async Task ClaimAndPositionVehicleAsync(
        string baseUrl, string repEmail, string vehicleId, double latitude, double longitude)
    {
        await ClaimVehicleAsAsync(baseUrl, repEmail, vehicleId);
        await PositionVehicleAtAsync(baseUrl, vehicleId, latitude, longitude);
    }

    public static Guid AssignRequestViaRep(
        string baseUrl, string repEmail, string vehicleId,
        string requesterEmail, string dtcId, double latitude, double longitude) =>
        AssignRequestViaRepAsync(baseUrl, repEmail, vehicleId, requesterEmail, dtcId, latitude, longitude)
            .GetAwaiter().GetResult();

    /// <summary>
    /// Drives the given rep's already-<b>Assigned</b> request through to <b>Completed</b> (Desktop analogue of
    /// the Playwright AC-4 completion): re-posts the rep's vehicle AT the request site so the backend advances
    /// the rep EnRoute → Within15Miles (proximity recompute on the position update), then — as the rep —
    /// <c>POST /rep/arrive</c> (→ OnSite, request InProgress) and <c>POST /rep/complete</c> (→ Available,
    /// request Completed). The complete handler fires <c>ServiceRequestCompleted</c> to <c>dealer:{id}</c>, so
    /// the dispatcher rail removes the card in real time. Synchronous wrapper for NUnit test bodies; throws on
    /// any step failure. Must be called only after <see cref="AssignRequestViaRep"/> has assigned this rep.
    /// </summary>
    public static void CompleteAssignedRequestViaRep(
        string baseUrl, string repEmail, string vehicleId, double latitude, double longitude) =>
        CompleteAssignedRequestViaRepAsync(baseUrl, repEmail, vehicleId, latitude, longitude)
            .GetAwaiter().GetResult();

    private static async Task<Guid> AssignRequestViaRepAsync(
        string baseUrl, string repEmail, string vehicleId,
        string requesterEmail, string dtcId, double latitude, double longitude)
    {
        // 1. Claim the dedicated vehicle as the rep (409 = already held by this rep on a warm backend, tolerated).
        await ClaimVehicleAsAsync(baseUrl, repEmail, vehicleId);

        // 2. Position the vehicle at the FAR pin (>15 mi from the request — see FarLatitudeOffsetDegrees) — NOT
        //    at the request site. There is no matching-radius cap, so being the NEAREST Available rep is enough
        //    to win the offer (every other rep is parked at the distant holding point), and starting far keeps
        //    the rep clear of the ONE-WAY Within15Miles proximity latch: a rep positioned AT the request (0 mi)
        //    latches to Within15Miles on accept and can never return to EnRoute (BUG-059), which is
        //    redirect-INELIGIBLE.
        await PositionVehicleAtAsync(baseUrl, vehicleId, latitude + FarLatitudeOffsetDegrees, longitude);

        // 3. Submit the request as the requester — matching offers it to the nearest available rep (this rep).
        var requestId = await SubmitServiceRequestAsync(baseUrl, requesterEmail, dtcId, latitude, longitude);

        // 4. As the rep, wait for the offer to materialise, then accept it → ServiceRequestAssigned is emitted.
        //    The rep is at the far pin, so acceptance leaves it EnRoute (never Within15Miles-latched).
        await AcceptPendingOfferAsRepAsync(baseUrl, repEmail);

        // 5. Re-broadcast the rep's EnRoute position to the dispatcher fleet. A live dispatcher learns a rep's
        //    state only from the VehiclePositionUpdated stream, which a real system emits every ~3 s; the login
        //    snapshot predates this assignment. This backend-only arrange has no such driver, so post one more
        //    position at the SAME far pin (still >15 mi → stays EnRoute), then gate on the backend actually
        //    reporting EnRoute. Together with the real-time active-request tier the queue ViewModel derives from
        //    ServiceRequestAssigned (FE-005 cycle 3), this surfaces the Redirect button with NO snapshot reload.
        //    Arrange-only — it emulates the position stream a live system always produces.
        await PositionVehicleAtAsync(baseUrl, vehicleId, latitude + FarLatitudeOffsetDegrees, longitude);
        await WaitForVehicleEnRouteAsync(baseUrl, vehicleId);

        return requestId;
    }

    // ~34 mi SOUTH of the request. Each scenario's Gold target sits ~0.4° NORTH of its Silver request, so a
    // far pin to the NORTH would land the rep within ~12 mi of its own Gold site and trip the one-way
    // Within15Miles proximity latch (BUG-059) relative to that Gold — which is redirect-INELIGIBLE. Pinning
    // SOUTH keeps the rep >15 mi from BOTH its Silver (assigned) and every Gold site, so it stays EnRoute; the
    // absent matching-radius cap means it is still the nearest Available rep to its Silver request.
    private const double FarLatitudeOffsetDegrees = -0.5;

    /// <summary>
    /// Re-broadcasts the vehicle's EnRoute position at the far pin (<see cref="FarLatitudeOffsetDegrees"/> north
    /// of the request site — &gt;15 mi, so the rep stays EnRoute, not the redirect-ineligible Within15Miles).
    /// A live dispatcher learns a rep's state only from the VehiclePositionUpdated stream, which a real system
    /// emits every ~3 s; a backend-only run has no such driver, so the Desktop redirect arrange calls this on
    /// each poll lap until the Redirect button surfaces (mirroring the fleet-map fixture's re-POST-each-lap).
    /// </summary>
    public static void RebroadcastEnRoutePosition(
        string baseUrl, string vehicleId, double requestLatitude, double requestLongitude) =>
        PositionVehicleAtAsync(
            baseUrl, vehicleId, requestLatitude + FarLatitudeOffsetDegrees, requestLongitude)
            .GetAwaiter().GetResult();

    // Deterministic readiness gate: poll GET /dispatcher/fleet until the vehicle reports EnRoute, so the UI wait
    // that follows starts from a confirmed backend precondition rather than racing the position broadcast.
    private static async Task WaitForVehicleEnRouteAsync(string baseUrl, string vehicleId)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, DispatcherEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var targetId = Guid.Parse(vehicleId);
        const int maxAttempts = 30;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var fleet = await client.GetFromJsonAsync<List<DispatcherFleetEntry>>("/dispatcher/fleet", JsonOptions)
                        ?? new List<DispatcherFleetEntry>();
            if (fleet.Any(e => e.VehicleId == targetId && e.State == "EnRoute"))
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new InvalidOperationException(
            $"Vehicle {vehicleId} did not report EnRoute in GET /dispatcher/fleet within {maxAttempts * 500 / 1000}s " +
            "after the post-accept far reposition — the redirect precondition (an EnRoute rep on a lower-tier job) " +
            "could not be established.");
    }

    private static async Task CompleteAssignedRequestViaRepAsync(
        string baseUrl, string repEmail, string vehicleId, double latitude, double longitude)
    {
        using var repClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var repToken = await LoginAsync(repClient, repEmail);
        repClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", repToken);

        // Advance the rep EnRoute → Within15Miles by re-posting its vehicle at the request site (proximity is
        // recomputed on each position update for an EnRoute/Within15Miles rep), then arrive → complete. Arrive
        // requires Within15Miles, so retry it briefly in case the position write and the state read race.
        const int maxArriveAttempts = 20;
        for (var attempt = 0; attempt < maxArriveAttempts; attempt++)
        {
            await PositionVehicleAtAsync(baseUrl, vehicleId, latitude, longitude);

            var arrive = await repClient.PostAsync("/rep/arrive", content: null);
            if (arrive.IsSuccessStatusCode)
            {
                break;
            }

            if (attempt == maxArriveAttempts - 1)
            {
                var content = await arrive.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"POST /rep/arrive did not succeed within {maxArriveAttempts} attempts for {repEmail} " +
                    $"({(int)arrive.StatusCode} {arrive.StatusCode}): {content}");
            }

            await Task.Delay(250);
        }

        var complete = await repClient.PostAsync("/rep/complete", content: null);
        if (!complete.IsSuccessStatusCode)
        {
            var content = await complete.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"POST /rep/complete failed for {repEmail} " +
                $"({(int)complete.StatusCode} {complete.StatusCode}): {content}");
        }
    }

    private static async Task ClaimVehicleAsAsync(string baseUrl, string repEmail, string vehicleId)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, repEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/vehicles/{vehicleId}/claim", content: null);

        // A 409 Conflict means this rep already holds this vehicle (warm/reused backend) — the desired end
        // state, tolerated. Any other non-success is fatal so a broken precondition surfaces immediately.
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"POST /vehicles/{vehicleId}/claim failed for {repEmail} " +
            $"({(int)response.StatusCode} {response.StatusCode}): {body}");
    }

    private static async Task PositionVehicleAtAsync(
        string baseUrl, string vehicleId, double latitude, double longitude)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, SimulatorEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var body = new { latitude, longitude, timestamp = DateTime.UtcNow };
        var response = await client.PostAsJsonAsync($"/vehicles/{vehicleId}/position", body);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"POST /vehicles/{vehicleId}/position failed " +
                $"({(int)response.StatusCode} {response.StatusCode}): {content}");
        }
    }

    private static async Task AcceptPendingOfferAsRepAsync(string baseUrl, string repEmail)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var token = await LoginAsync(client, repEmail);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Bounded poll for the offer to appear (match → offer is quick, but not necessarily synchronous with
        // the submit response), then accept it. 30 × 500 ms = 15 s covers the offer-creation delay comfortably.
        const int maxAttempts = 30;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var pending = await client.GetAsync("/job-offers/pending");
            if (pending.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var offer = await pending.Content.ReadFromJsonAsync<PendingOfferResponse>(JsonOptions);
                if (offer is not null && offer.OfferId != Guid.Empty)
                {
                    var accept = await client.PostAsync($"/job-offers/{offer.OfferId}/accept", content: null);
                    if (accept.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var content = await accept.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        $"POST /job-offers/{offer.OfferId}/accept failed for {repEmail} " +
                        $"({(int)accept.StatusCode} {accept.StatusCode}): {content}");
                }
            }

            await Task.Delay(500);
        }

        throw new InvalidOperationException(
            $"No pending job offer appeared for {repEmail} within {maxAttempts * 500 / 1000}s — the request " +
            "was never offered to the dedicated rep (matching precondition broken).");
    }

    private sealed record PendingOfferResponse(Guid OfferId);

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
