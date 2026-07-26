using ServiceDelivery.Client.E2E.Helpers;

namespace ServiceDelivery.Client.E2E;

/// <summary>
/// FE-004 Playwright coverage (ACs 1, 2, 3, 4, 5) against the live Web host. Drives the dispatcher ACTIVE
/// REQUESTS queue as a black box, asserting only on <c>data-testid</c> selectors. Active requests are seeded
/// via the backend (<c>POST /service-requests</c> as a seeded requester) so the queue has cards to render; the
/// real-time scenarios exercise the DispatchHub delivery path (a card appears on <c>ServiceRequestPending</c>
/// and disappears on <c>ServiceRequestCompleted</c> with no page reload). Authored but NOT executed in the
/// /master pipeline — run via <c>scripts/local/test-playwright.sh</c> (or <c>test-e2e.sh</c>) against a live
/// system.
/// </summary>
[TestFixture]
public sealed class DispatcherRequestQueueTests : E2ETestBase
{
    // Iowa map centre — an in-range site so a submitted request can be matched/assigned. Used by the
    // presence-only scenarios (AC-1/AC-2), which assert a card RENDERS and do not need coordinate uniqueness.
    private const double IowaLat = 41.60;
    private const double IowaLng = -93.60;

    // Distinct per-scenario coordinates for the two real-time scenarios. The completion helper
    // (CompleteAssignedRequestAt) selects the rep to drive by its active request's LOCATION, so a scenario that
    // completes a job must submit at coordinates no other scenario (or sibling fixture, or a stale prior request)
    // shares — otherwise it can complete the WRONG request and leave the watched card visible. There is no
    // matching-radius cap, so any coordinate is matchable; these are simply unique markers well away from the
    // shared Iowa centre (41.60,-93.60) and the sibling fixtures' sites.
    private const double AssignAddLat = 41.15;   // AC-3 real-time add (matched → assigned)
    private const double AssignAddLng = -91.35;
    private const double CompletionLat = 41.25;  // AC-4 real-time remove (assigned → completed)
    private const double CompletionLng = -91.55;

    // Seeded DTC-001 (matches the seeded HydraulicTool fleet).
    private const string Dtc001Id = "20000000-0000-0000-0000-000000000001";

    private static string BackendBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BACKEND_URL") ?? "http://localhost:5180";

    private static string GoldRequesterEmail =>
        Environment.GetEnvironmentVariable("E2E_GOLD_REQUESTER_EMAIL") ?? "gold1@example.com";

    private static string SilverRequesterEmail =>
        Environment.GetEnvironmentVariable("E2E_SILVER_REQUESTER_EMAIL") ?? "silver1@example.com";

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenQueueLoads_ThenCardShowsRequesterNameAndDtcTitle()
    {
        // Arrange — AC-2: seed one active request, then log in so the snapshot load renders its card.
        var requestId = BackendApiHelper.SubmitServiceRequest(
            BackendBaseUrl, GoldRequesterEmail, Dtc001Id, IowaLat, IowaLng);
        await LoginAsDispatcherAsync();

        // Act — wait for THIS request's card to render in the queue list.
        var card = await Page.WaitForSelectorAsync(
            $"[data-testid='request-card-{requestId}']", new() { Timeout = 15_000 });

        // Assert — the requester name and DTC title lines are populated.
        Assert.That(await card!.IsVisibleAsync(), Is.True);
        var name = await card.QuerySelectorAsync("[data-testid='reqcard-name']");
        var dtc = await card.QuerySelectorAsync("[data-testid='reqcard-dtc']");
        Assert.That((await name!.TextContentAsync())?.Trim(), Is.Not.Empty);
        Assert.That((await dtc!.TextContentAsync())?.Trim(), Is.Not.Empty);
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenQueueLoadsWithMixedTierRequests_ThenGoldCardAppearsFirst()
    {
        // Arrange — AC-1: seed a Silver then a Gold request; the queue must sort Gold above Silver regardless
        // of submission order.
        BackendApiHelper.SubmitServiceRequest(BackendBaseUrl, SilverRequesterEmail, Dtc001Id, IowaLat, IowaLng);
        var goldId = BackendApiHelper.SubmitServiceRequest(
            BackendBaseUrl, GoldRequesterEmail, Dtc001Id, IowaLat, IowaLng);
        await LoginAsDispatcherAsync();

        // Act — wait for the queue to have cards, then read the first card's tier badge.
        await Page.WaitForSelectorAsync(
            $"[data-testid='request-card-{goldId}']", new() { Timeout = 15_000 });

        // Assert — the first card in the list carries the GOLD tier badge.
        var firstBadge = await Page.WaitForSelectorAsync(
            "[data-testid='dispatcher-queue-list'] .sd-reqcard:first-child [data-testid='reqcard-tier-badge']");
        Assert.That((await firstBadge!.TextContentAsync())?.Trim(), Does.Contain("GOLD"));
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenANewRequestIsMatchedAndAssigned_ThenItAppearsInTheQueueInRealTime()
    {
        // Arrange — AC-3 (real-time ADD over DispatchHub). Position the fleet in range and log in FIRST, so the
        // dispatcher's initial snapshot does NOT contain the request submitted below — the card can therefore
        // ONLY arrive via a live hub event, making this a genuine real-time guard (a dead DispatchHub never
        // delivers it and the test times out).
        //
        // The realistic, deterministic add path on this backend is ServiceRequestAssigned. There is no
        // matching-radius cap, so a submitted, in-range DTC-001 request is matched to the nearest available rep
        // and — with the E2E run forcing Simulator__AutoDeclineRatePercent=0 — that rep accepts, so the backend
        // emits ServiceRequestAssigned to the dealer group and the queue must add the card in real time (the
        // upsert path). (A genuinely unmatched request would instead emit ServiceRequestPending — that add path
        // is covered deterministically by the DispatcherRequestQueueViewModel unit tests.)
        BackendApiHelper.PositionFleetAt(BackendBaseUrl, AssignAddLat, AssignAddLng);
        await LoginAsDispatcherAsync();

        // Wait for the dispatcher's DispatchHub connection to be established AND joined to its dealer:{id} group
        // BEFORE submitting, so the request-lifecycle event fired after the accept is actually delivered to this
        // rail. data-dispatch-hub-connected flips to 'true' only after the rail's ViewModel completes
        // StartHubAsync (the connect is awaited) — a deterministic readiness signal, not a fixed sleep.
        // State=Attached (not the default Visible): the queue list is empty at this point, so it collapses to
        // zero size and Playwright would consider it "hidden" — we only care that the readiness attribute is set.
        await Page.WaitForSelectorAsync(
            "[data-testid='dispatcher-queue-list'][data-dispatch-hub-connected='true']",
            new() { State = WaitForSelectorState.Attached, Timeout = 15_000 });

        // Act
        var requestId = BackendApiHelper.SubmitServiceRequest(
            BackendBaseUrl, GoldRequesterEmail, Dtc001Id, AssignAddLat, AssignAddLng);

        // Assert — bounded poll for the new card. The assignment (reviewing delay + accept + hub delivery)
        // arrives well within the 15 s budget; a dead DispatchHub never delivers it, so this times out and
        // fails — the real-time guard.
        var card = await Page.WaitForSelectorAsync(
            $"[data-testid='request-card-{requestId}']", new() { Timeout = 15_000 });
        Assert.That(await card!.IsVisibleAsync(), Is.True);
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenServiceRequestCompletedEventReceived_ThenCardDisappearsFromQueue()
    {
        // Arrange — AC-4: position the fleet in range and submit at CompletionLat/Lng (coordinates unique to
        // this scenario) so a rep is assigned, log in, and wait for the card. The unique site matters: the
        // completion helper below selects the rep to drive by its active request's LOCATION, so sharing coords
        // with another request (a sibling fixture or a stale prior submit) would let it complete the wrong job
        // and leave this card visible (the observed AC-4 flake). Then drive the assigned job to completion so
        // the backend emits ServiceRequestCompleted for THIS request.
        BackendApiHelper.PositionFleetAt(BackendBaseUrl, CompletionLat, CompletionLng);
        var requestId = BackendApiHelper.SubmitServiceRequest(
            BackendBaseUrl, GoldRequesterEmail, Dtc001Id, CompletionLat, CompletionLng);
        await LoginAsDispatcherAsync();

        var cardSelector = $"[data-testid='request-card-{requestId}']";
        await Page.WaitForSelectorAsync(cardSelector, new() { Timeout = 15_000 });

        // The card renders from the REST snapshot, which does NOT require the hub — so card-present alone does
        // not prove the DispatchHub connection is live. Wait for the connection to join its dealer:{id} group
        // BEFORE completing the job, otherwise the one-shot ServiceRequestCompleted removal is emitted before
        // the subscription is live and never reaches this rail (the card stays visible). Deterministic readiness
        // signal (StartHubAsync's connect resolved), not a fixed sleep. State=Attached for the same reason as
        // AC-3 — the readiness assertion is on the attribute, independent of the list's visible size.
        await Page.WaitForSelectorAsync(
            "[data-testid='dispatcher-queue-list'][data-dispatch-hub-connected='true']",
            new() { State = WaitForSelectorState.Attached, Timeout = 15_000 });

        // Act — complete the assigned job at THIS request's unique site (so the helper drives the rep serving
        // this request, not any other rep on the shared fleet).
        BackendApiHelper.CompleteAssignedRequestAt(BackendBaseUrl, CompletionLat, CompletionLng);

        // Assert — the card disappears from the queue with no page reload (ServiceRequestCompleted removal).
        await Page.WaitForSelectorAsync(
            cardSelector, new() { State = WaitForSelectorState.Detached, Timeout = 15_000 });
        Assert.That(await Page.Locator(cardSelector).CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenViewportIsAt1440And1280Px_ThenQueueRailIsVisibleBesideMap()
    {
        // Arrange — AC-5: at Desktop (1440) and Web (1280) widths the map and the queue rail sit side by side.
        await LoginAsDispatcherAsync();
        await Page.WaitForSelectorAsync("[data-testid='dispatcher-dashboard']");

        foreach (var width in new[] { 1440, 1280 })
        {
            // Act
            await Page.SetViewportSizeAsync(width, 900);

            // Assert — both columns visible, and the rail sits to the RIGHT of the map (side-by-side layout).
            var mapBox = await Page.Locator("[data-testid='dispatcher-map-column']").BoundingBoxAsync();
            var railBox = await Page.Locator("[data-testid='dispatcher-queue-rail']").BoundingBoxAsync();
            Assert.That(mapBox, Is.Not.Null, $"map column should be laid out at {width}px");
            Assert.That(railBox, Is.Not.Null, $"queue rail should be laid out at {width}px");
            Assert.That(railBox!.X, Is.GreaterThan(mapBox!.X), $"rail should be right of the map at {width}px");
        }
    }

    [Test]
    public async Task GivenAuthenticatedDispatcher_WhenViewportNarrowedTo740Px_ThenQueueRailStacksBelowMap()
    {
        // Arrange — AC-5: below the 768px breakpoint the rail stacks BELOW the map (single column).
        await LoginAsDispatcherAsync();
        await Page.WaitForSelectorAsync("[data-testid='dispatcher-dashboard']");

        // Act
        await Page.SetViewportSizeAsync(740, 900);

        // Assert — the rail's top edge is below the map's top edge (vertically stacked, not side by side).
        var mapBox = await Page.Locator("[data-testid='dispatcher-map-column']").BoundingBoxAsync();
        var railBox = await Page.Locator("[data-testid='dispatcher-queue-rail']").BoundingBoxAsync();
        Assert.That(mapBox, Is.Not.Null);
        Assert.That(railBox, Is.Not.Null);
        Assert.That(railBox!.Y, Is.GreaterThan(mapBox!.Y), "rail should stack below the map at 740px");
    }
}
