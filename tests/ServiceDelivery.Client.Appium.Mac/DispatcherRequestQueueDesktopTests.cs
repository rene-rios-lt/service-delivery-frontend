using System.Collections.ObjectModel;
using ServiceDelivery.Client.Appium.Mac.Helpers;

namespace ServiceDelivery.Client.Appium.Mac;

/// <summary>
/// FE-004 — the live Desktop (Mac Catalyst) gate for the dispatcher ACTIVE REQUESTS queue. The Dispatcher
/// persona runs on BOTH Web and Desktop from the same shared Client.UI/Client.Core code, so every meaningful
/// Playwright (web) scenario in <c>DispatcherRequestQueueTests</c> has a mirrored Desktop scenario here — and
/// Desktop is a DIFFERENT runtime (MAUI Blazor Hybrid / WKWebView on Mac Catalyst) from browser WASM, so
/// web-green does not imply WebView-green (the BUG-020→BUG-022 / BUG-031 host-parity lesson extended to E2E).
/// <list type="bullet">
/// <item><b>AC-1</b> — the queue sorts Gold above Silver (assert AX document order of the two seeded cards).</item>
/// <item><b>AC-2</b> — a seeded card's fields (requester name + DTC title) are AX-accessible.</item>
/// <item><b>AC-3</b> — a request matched-and-assigned AFTER login appears as a card in real time (the DispatchHub
///   <c>ServiceRequestAssigned</c> upsert path — the HIGHEST-VALUE mirror: it proves the rail's
///   StateChanged/hub render-timing fix in the WKWebView runtime, not just the browser).</item>
/// <item><b>AC-4</b> — an assigned request driven to Completed has its card removed in real time
///   (<c>ServiceRequestCompleted</c>).</item>
/// <item><b>AC-5</b> — <b>NOT mirrored on Desktop (web-only by design)</b>: the AC is explicitly "the layout
///   reflows on narrower <i>web</i> widths"; the Desktop host is a fixed-size native window with no browser
///   viewport to resize. Covered by the Playwright viewport scenarios only.</item>
/// </list>
/// <para>
/// NATIVE mac2 (no WebView context — see <see cref="MacDesktopTestBase"/>): assertions are made against the AX
/// tree, not the DOM. A card's <c>data-testid</c> is AX-invisible, but its rendered text (requester name, DTC
/// title) surfaces as accessible static text, so those are the anchors. The DispatchHub connection state — the
/// deterministic readiness signal the web tests read from the DOM-only <c>data-dispatch-hub-connected</c>
/// attribute — is surfaced to the AX tree by the rail's sr-only status twin ("Live request updates connected"),
/// which the real-time scenarios wait for before firing their one-shot backend event (lost with no retry if
/// emitted before the dispatcher joins its <c>dealer:{id}</c> group). The suite runs backend-only
/// (<c>SD_SKIP_SIMULATOR=1</c>), so there is no rep-operating simulator: the assign/complete arrange claims a
/// dedicated rep+vehicle and accepts/drives the job itself via <see cref="BackendApiHelper"/>. One shared
/// mac2 session per fixture (launching the Desktop app is slow). Authored AND run live via
/// <c>scripts/local/test-appium-mac.sh</c>.
/// </para>
/// </summary>
[TestFixture]
public sealed class DispatcherRequestQueueDesktopTests : MacDesktopTestBase
{
    // Iowa map centre — a valid dealer coordinate for the AC-1/AC-2 snapshot-seeded requests.
    private const double IowaLat = 41.60;
    private const double IowaLng = -93.60;

    // Distinct per-scenario coordinates for the two real-time scenarios (near the Iowa region the Playwright
    // suite uses, well away from the Iowa centre and from each other). The dedicated rep for each scenario is
    // positioned AT its request's coordinates so it is the nearest qualified candidate (there is no
    // matching-radius cap), guaranteeing the offer lands on that rep and not on any other Available rep.
    private const double AssignLat = 41.10;   // AC-3 real-time add (matched → assigned)
    private const double AssignLng = -91.00;
    private const double CompleteLat = 41.30;  // AC-4 real-time remove (assigned → completed)
    private const double CompleteLng = -91.40;

    // Seeded DTC-001 ("Hydraulic system fault") — matches the seeded HydraulicTool fleet (V-001..V-007).
    private const string Dtc001Id = "20000000-0000-0000-0000-000000000001";

    // Seeded requesters (distinct per scenario so each card's name uniquely identifies it in the shared queue).
    private const string GoldRequesterEmail = "gold1@example.com";
    private const string SeededRequesterName = "Gold User 1";
    private const string SeededDtcTitle = "Hydraulic system fault";
    private const string SilverRequesterEmail = "silver1@example.com";
    private const string SilverRequesterName = "Silver User 1";
    private const string AssignRequesterEmail = "silver2@example.com";  // AC-3
    private const string AssignRequesterName = "Silver User 2";
    private const string CompleteRequesterEmail = "bronze1@example.com"; // AC-4
    private const string CompleteRequesterName = "Bronze User 1";

    // Dedicated rep+vehicle per real-time scenario (both carry HydraulicTool). rep1/V-001 is reserved by the
    // sibling fleet-map fixture, so these use rep2/rep3 to avoid cross-fixture rep contention.
    private const string AssignRepEmail = "rep3@dealer.com";            // AC-3
    private const string AssignVehicleId = "30000000-0000-0000-0000-000000000003";
    private const string CompleteRepEmail = "rep2@dealer.com";          // AC-4
    private const string CompleteVehicleId = "30000000-0000-0000-0000-000000000002";

    // AX anchors for the card's rendered text (plain <div> text WebKit may nest under a wrapping group, so
    // match any element type by exact value/label — the same pattern the base uses for the dashboard head).
    private static readonly By RequesterNameText = NameText(SeededRequesterName);
    private static readonly By DtcTitleText = NameText(SeededDtcTitle);

    // The rail's AX-exposed DispatchHub-connected readiness twin (sr-only text mirroring the web
    // data-dispatch-hub-connected attribute, which mac2 cannot read — no WebView context).
    private static readonly By HubConnectedStatus = NameText("Live request updates connected");

    // AC-1 order anchor: either seeded card's requester name; FindElements returns them in AX document order.
    private static readonly By GoldOrSilverName = By.XPath(
        $"//*[@value=\"{SeededRequesterName}\" or @label=\"{SeededRequesterName}\" " +
        $"or @value=\"{SilverRequesterName}\" or @label=\"{SilverRequesterName}\"]");

    [OneTimeSetUp]
    public void SeedRequestsAndLogIn()
    {
        try
        {
            // Seed a Gold and a Silver active request BEFORE login so the dashboard's queue LoadAsync
            // GET /service-requests snapshot already carries both — their cards render on mount with no hub
            // event (backend-only, so they stay active). The Gold anchors AC-2; the Gold+Silver pair anchors
            // AC-1's ordering. Login once for the shared session (launching the app is slow).
            BackendApiHelper.SubmitServiceRequest(
                MacDesktopConfig.BackendBaseUrl, GoldRequesterEmail, Dtc001Id, IowaLat, IowaLng);
            BackendApiHelper.SubmitServiceRequest(
                MacDesktopConfig.BackendBaseUrl, SilverRequesterEmail, Dtc001Id, IowaLat, IowaLng);
            LoginAsDispatcher();
        }
        catch
        {
            DumpAxTreeIfRequested("SeedRequestsAndLogIn");
            throw;
        }
    }

    [Test]
    public void GivenDesktopDispatcher_WhenQueueLoadsWithASeededRequest_ThenAtLeastOneRequestCardIsAXAccessible()
    {
        // Arrange — the fixture seeded active requests and logged in; their cards render from the queue
        // snapshot on dashboard mount.

        // Act — bounded poll (the 15 s SignalR budget) for the card's requester-name text to surface in the AX
        // tree, proving at least one request card rendered on the Desktop host.
        var nameShown = WaitForSignalR(d => d.FindElements(RequesterNameText).Count > 0);

        // Assert
        Assert.That(
            nameShown, Is.True,
            "At least one request card should render on the Desktop dispatcher queue (asserted via its AX-exposed requester-name text).");
    }

    [Test]
    public void GivenDesktopDispatcher_WhenQueueLoadsWithASeededRequest_ThenCardTextIncludesRequesterNameAndDtcTitle()
    {
        // Arrange — as above.

        // Act — wait for both the requester name and the DTC title to be present in the AX tree.
        WaitForSignalR(d => d.FindElements(RequesterNameText).Count > 0);
        var dtcShown = WaitForSignalR(d => d.FindElements(DtcTitleText).Count > 0);

        // Assert — both card fields are AX-accessible.
        Assert.That(ExistsNow(RequesterNameText), Is.True, "The card's requester name should be AX-accessible.");
        Assert.That(dtcShown, Is.True, "The card's DTC title should be AX-accessible.");
    }

    [Test]
    public void GivenDesktopDispatcher_WhenTheQueueHoldsMixedTiers_ThenTheGoldCardSortsAboveTheSilverCard()
    {
        // Arrange — AC-1: the fixture seeded a Silver AND a Gold active request; the queue must sort Gold above
        // Silver regardless of submission order. Both cards render from the snapshot.

        // Act — wait until both cards' requester-name texts are present, captured in AX document order. The
        // combined locator matches either name; FindElements returns matches top-to-bottom, i.e. card order.
        var ordered = WaitForSignalR(d =>
        {
            ReadOnlyCollection<AppiumElement> els = d.FindElements(GoldOrSilverName);
            return els.Count >= 2 ? els : null;
        })!;

        // Assert — the FIRST of the two tier-named cards in the queue is the Gold one.
        Assert.That(
            AccessibleName(ordered[0]), Is.EqualTo(SeededRequesterName),
            "The Gold request card should sort above the Silver card on the Desktop dispatcher queue (AC-1).");
    }

    [Test]
    public void GivenDesktopDispatcher_WhenANewRequestIsMatchedAndAssigned_ThenItsCardAppearsInRealTime()
    {
        // Arrange — AC-3 (real-time ADD over DispatchHub, the HIGHEST-VALUE Desktop mirror). The dispatcher is
        // already logged in (fixture). Wait for the rail's AX-exposed readiness twin so the DispatchHub
        // connection has joined its dealer:{id} group BEFORE the assign is driven — otherwise the one-shot
        // ServiceRequestAssigned is emitted before the subscription is live and is lost with no retry. This is
        // the AX analogue of the web test's data-dispatch-hub-connected gate.
        WaitForSignalR(d => d.FindElements(HubConnectedStatus).Count > 0);

        var newCardName = NameText(AssignRequesterName);
        // Nothing for this distinct requester should exist yet, so the card below can ONLY arrive via the live
        // hub event (a dead DispatchHub never delivers it and the wait times out — the real-time guard).
        Assert.That(ExistsNow(newCardName), Is.False, "The AC-3 card must not exist before the request is submitted.");

        // Act — matched-and-assigned real-time add: claim a dedicated rep at the request site, submit, and
        // accept the offer as that rep (backend-only, so no simulator accepts on its own). On accept the backend
        // emits ServiceRequestAssigned to the dealer group and the rail must ADD the card with no reload.
        BackendApiHelper.AssignRequestViaRep(
            MacDesktopConfig.BackendBaseUrl, AssignRepEmail, AssignVehicleId,
            AssignRequesterEmail, Dtc001Id, AssignLat, AssignLng);

        // Assert — bounded poll for the new card's requester-name text to surface in the AX tree.
        var appeared = WaitForSignalR(d => d.FindElements(newCardName).Count > 0);
        Assert.That(
            appeared, Is.True,
            "A newly matched-and-assigned request should appear as a card on the Desktop dispatcher queue in real time (AC-3).");
    }

    [Test]
    public void GivenDesktopDispatcher_WhenAnAssignedRequestIsCompleted_ThenItsCardDisappearsInRealTime()
    {
        // Arrange — AC-4 (real-time REMOVE over DispatchHub). Wait for the hub-connected readiness twin, then
        // drive a distinct request to Assigned via a dedicated rep and confirm its card is present in the AX
        // tree (so the removal below is observable). The card arrives via the ServiceRequestAssigned upsert.
        WaitForSignalR(d => d.FindElements(HubConnectedStatus).Count > 0);

        BackendApiHelper.AssignRequestViaRep(
            MacDesktopConfig.BackendBaseUrl, CompleteRepEmail, CompleteVehicleId,
            CompleteRequesterEmail, Dtc001Id, CompleteLat, CompleteLng);

        var cardName = NameText(CompleteRequesterName);
        var present = WaitForSignalR(d => d.FindElements(cardName).Count > 0);
        Assert.That(present, Is.True, "The assigned request's card should be present before completion (AC-4 precondition).");

        // Act — drive the assigned job to completion as the dedicated rep; the backend emits
        // ServiceRequestCompleted to the dealer group.
        BackendApiHelper.CompleteAssignedRequestViaRep(
            MacDesktopConfig.BackendBaseUrl, CompleteRepEmail, CompleteVehicleId, CompleteLat, CompleteLng);

        // Assert — the card disappears from the queue with no page reload (ServiceRequestCompleted removal).
        var removed = WaitForSignalR(d => d.FindElements(cardName).Count == 0);
        Assert.That(
            removed, Is.True,
            "A completed request's card should disappear from the Desktop dispatcher queue in real time (AC-4).");
    }

    // Any element type carrying EXACTLY the given text — matches either the AX value or label, since WebKit may
    // expose a plain <div>'s text as either (and may nest it under a wrapping group).
    private static By NameText(string text) =>
        By.XPath($"//*[@value=\"{text}\" or @label=\"{text}\"]");
}
