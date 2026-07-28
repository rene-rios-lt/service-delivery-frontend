using System.Text.Json;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// Pure xUnit tests for the FE-005 redirect extension of <see cref="DispatcherRequestQueueViewModel"/> (ACs 1,
/// 2, 3, 4) plus the ADR-0011 captured-payload wire-contract proofs for <see cref="RedirectRepResultDto"/> and
/// the <c>redirectCooldownExpiresAt</c> field added to <see cref="DispatcherFleetEntryDto"/>. Uses Moq for the
/// two collaborators (<see cref="IRedirectEligibilityService"/>, <see cref="IDispatcherRedirectService"/>) — no
/// rendering, no live hub. The eligibility service is mocked so the ViewModel's bridging / dialog / optimistic
/// state is tested in isolation from the eligibility RULES (those are covered by RedirectEligibilityServiceTests).
/// </summary>
public class DispatcherRequestQueueViewModelRedirectTests
{
    private readonly Mock<IActiveRequestQueueService> _queueService = new();
    private readonly Mock<IDispatchHubService> _hub = new();
    private readonly Mock<IRedirectEligibilityService> _eligibility = new();
    private readonly Mock<IDispatcherRedirectService> _redirectService = new();

    private static readonly Guid GoldRequestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");
    private static readonly Guid SilverRequestId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid RepId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    private DispatcherRequestQueueViewModel CreateViewModel() =>
        new(_queueService.Object, _hub.Object, _eligibility.Object, _redirectService.Object);

    // A ViewModel wired with the REAL eligibility service (not the mock) so the real-time active-request tier
    // genuinely flows through the eligibility RULE — a mock would decide eligibility regardless of the derived
    // tier and so could not prove the tier reached the rule.
    private DispatcherRequestQueueViewModel CreateViewModelWithRealEligibility() =>
        new(_queueService.Object, _hub.Object, new RedirectEligibilityService(), _redirectService.Object);

    private static ActiveRequestEntry GoldEntry() =>
        new(GoldRequestId, "Marcus Webb", ServiceTier.Gold, "Transmission Control Fault", "Pending", null,
            DateTimeOffset.UtcNow);

    private static ActiveRequestEntry SilverEntry() =>
        new(SilverRequestId, "Dana Cole", ServiceTier.Silver, "Hydraulic Pressure Loss", "Pending", null,
            DateTimeOffset.UtcNow);

    private static FleetVehicleEntry EnRouteRepOnSilver() =>
        new("30000000-0000-0000-0000-000000000007", "IA-4471", "EnRoute", RepId, "J. Tran",
            41.8781, -93.0977, "Hydraulic Pressure Loss", "Silver", false, null);

    // The rep is present in the fleet and already EnRoute, but the GET /dispatcher/fleet snapshot that loaded at
    // dispatcher login PREDATES their assignment, so it carries no active-request tier (null). This is the exact
    // real-world condition: a rep assigned AFTER the dispatcher opened the board.
    private static FleetVehicleEntry EnRouteRepWithNoTierYet() =>
        new("30000000-0000-0000-0000-000000000007", "IA-4471", "EnRoute", RepId, "J. Tran",
            41.8781, -93.0977, null, null, false, null);

    private static RedirectInfo InfoFor(Guid toRequestId, bool inCooldown = false) =>
        new(RepId, "J. Tran", ServiceTier.Silver, "Hydraulic Pressure Loss", ServiceTier.Gold,
            "Transmission Control Fault", inCooldown, toRequestId);

    private async Task<DispatcherRequestQueueViewModel> LoadedWith(params ActiveRequestEntry[] entries)
    {
        _queueService.Setup(s => s.GetActiveRequestsAsync()).ReturnsAsync(entries.ToList());
        var vm = CreateViewModel();
        await vm.LoadAsync();
        return vm;
    }

    // ---- AC-1: fleet data propagation → per-request redirect info ---------------------------------------

    [Fact]
    public async Task GivenQueueWithGoldEntryAndFleetEnRouteRepOnSilver_WhenUpdateFleetDataCalled_ThenGetRedirectInfoReturnsInfo()
    {
        // Arrange — the eligibility service (mocked) reports the Gold entry is redirectable from the fleet.
        var vm = await LoadedWith(GoldEntry());
        var fleet = new[] { EnRouteRepOnSilver() };
        _eligibility
            .Setup(e => e.FindEligibleRedirect(
                It.Is<ActiveRequestEntry>(r => r.RequestId == GoldRequestId), fleet))
            .Returns(InfoFor(GoldRequestId));

        // Act
        vm.UpdateFleetData(fleet);

        // Assert — the ViewModel surfaces the eligibility per queue entry.
        var info = vm.GetRedirectInfo(GoldRequestId);
        Assert.NotNull(info);
        Assert.Equal(RepId, info!.RepId);
        Assert.Equal(GoldRequestId, info.ToRequestId);
    }

    // ---- AC-1 / AC-5 (cycle 3): real-time active-request tier — eligibility WITHOUT a snapshot reload ----

    [Fact]
    public async Task GivenGoldQueuedAndRepEnRouteWithNoTier_WhenServiceRequestAssignedToLowerTierRequest_ThenRedirectBecomesEligibleWithoutSnapshotReload()
    {
        // Arrange — a Gold request and a lower-tier Silver request are both queued; the rep is present in the
        // fleet and EnRoute, but the login-time snapshot carried NO active-request tier for them, so no redirect
        // is eligible yet. Real eligibility service so the derived tier genuinely flows through the rule.
        _queueService.Setup(s => s.GetActiveRequestsAsync())
            .ReturnsAsync(new List<ActiveRequestEntry> { GoldEntry(), SilverEntry() });
        var vm = CreateViewModelWithRealEligibility();
        await vm.LoadAsync();
        vm.UpdateFleetData(new[] { EnRouteRepWithNoTierYet() });
        Assert.Null(vm.GetRedirectInfo(GoldRequestId)); // precondition: not eligible before the assignment

        // Act — the rep is assigned to the Silver request in real time (DispatchHub ServiceRequestAssigned).
        await vm.HandleServiceRequestAssignedAsync(
            new ServiceRequestAssignedPayload(SilverRequestId, RepId, "J. Tran", 12.5));

        // Assert — the Gold request now surfaces an eligible redirect (button would appear), no snapshot reload.
        var info = vm.GetRedirectInfo(GoldRequestId);
        Assert.NotNull(info);
        Assert.Equal(RepId, info!.RepId);
        Assert.Equal(ServiceTier.Silver, info.CurrentJobTier);
        Assert.Equal(GoldRequestId, info.ToRequestId);
    }

    [Fact]
    public async Task GivenRepMadeRedirectEligibleByAssignment_WhenTheirLowerTierRequestCompletes_ThenRedirectIsNoLongerEligible()
    {
        // Arrange — reach the eligible state via the real-time assignment path (as above).
        _queueService.Setup(s => s.GetActiveRequestsAsync())
            .ReturnsAsync(new List<ActiveRequestEntry> { GoldEntry(), SilverEntry() });
        var vm = CreateViewModelWithRealEligibility();
        await vm.LoadAsync();
        vm.UpdateFleetData(new[] { EnRouteRepWithNoTierYet() });
        await vm.HandleServiceRequestAssignedAsync(
            new ServiceRequestAssignedPayload(SilverRequestId, RepId, "J. Tran", 12.5));
        Assert.NotNull(vm.GetRedirectInfo(GoldRequestId)); // precondition: eligible

        // Act — the rep's lower-tier (Silver) request completes, freeing them.
        await vm.HandleServiceRequestCompletedAsync(new ServiceRequestCompletedPayload(SilverRequestId));

        // Assert — the rep is no longer a lower-tier redirect target for the Gold request (the inverse).
        Assert.Null(vm.GetRedirectInfo(GoldRequestId));
    }

    [Fact]
    public async Task GivenRepEnRouteWithNoTier_WhenAssignedToASameTierRequest_ThenRedirectStaysIneligible()
    {
        // Arrange — the real-time tier must be applied through the strict-lower-tier rule, not blindly: a rep
        // assigned to another GOLD request is NOT a lower-tier redirect target for a Gold request.
        _queueService.Setup(s => s.GetActiveRequestsAsync())
            .ReturnsAsync(new List<ActiveRequestEntry>
            {
                GoldEntry(),
                new(SilverRequestId, "Dana Cole", ServiceTier.Gold, "Coolant Temp High", "Pending", null,
                    DateTimeOffset.UtcNow),
            });
        var vm = CreateViewModelWithRealEligibility();
        await vm.LoadAsync();
        vm.UpdateFleetData(new[] { EnRouteRepWithNoTierYet() });

        // Act — assigned to the other Gold request (same tier as the target).
        await vm.HandleServiceRequestAssignedAsync(
            new ServiceRequestAssignedPayload(SilverRequestId, RepId, "J. Tran", 12.5));

        // Assert — same-tier is not redirectable.
        Assert.Null(vm.GetRedirectInfo(GoldRequestId));
    }

    [Fact]
    public async Task GivenAnEnRouteRepOnALowerTierJob_WhenAHigherTierRequestArrivesPending_ThenItIsImmediatelyRedirectEligible()
    {
        // Arrange — the fleet already holds an EnRoute rep whose real-time tier is Silver (assigned before this
        // request arrived); NO Gold request is queued yet. This is the symmetric real-time case: a dispatcher
        // already on the board must see the Redirect button for a request that arrives AFTER load, against an
        // already-eligible rep — without waiting for the next ~3s fleet poll to recompute.
        _queueService.Setup(s => s.GetActiveRequestsAsync())
            .ReturnsAsync(new List<ActiveRequestEntry> { SilverEntry() });
        var vm = CreateViewModelWithRealEligibility();
        await vm.LoadAsync();
        vm.UpdateFleetData(new[] { EnRouteRepWithNoTierYet() });
        await vm.HandleServiceRequestAssignedAsync(
            new ServiceRequestAssignedPayload(SilverRequestId, RepId, "J. Tran", 12.5));
        _queueService.Setup(s => s.GetRequestAsync(GoldRequestId)).ReturnsAsync(GoldEntry());

        // Act — a brand-new Gold request arrives on the DispatchHub (the Pending handler fetches the full entry).
        await vm.HandleServiceRequestPendingAsync(
            new ServiceRequestPendingPayload(GoldRequestId, "Gold", "Transmission Control Fault", "41.6,-93.6"));

        // Assert — the new higher-tier request is redirect-eligible immediately, no fleet poll required.
        Assert.NotNull(vm.GetRedirectInfo(GoldRequestId));
    }

    // ---- AC-2: dialog open / cancel ---------------------------------------------------------------------

    [Fact]
    public async Task GivenEligibleEntry_WhenShowRedirectDialogCalled_ThenActiveRedirectInfoMatchesEntry()
    {
        // Arrange — the Gold entry is eligible; opening the dialog surfaces its RedirectInfo.
        var vm = await LoadedWith(GoldEntry());
        var fleet = new[] { EnRouteRepOnSilver() };
        _eligibility.Setup(e => e.FindEligibleRedirect(It.IsAny<ActiveRequestEntry>(), fleet))
            .Returns(InfoFor(GoldRequestId));
        vm.UpdateFleetData(fleet);

        // Act
        vm.ShowRedirectDialog(GoldRequestId);

        // Assert
        Assert.NotNull(vm.ActiveRedirectInfo);
        Assert.Equal(GoldRequestId, vm.ActiveRedirectInfo!.ToRequestId);
    }

    [Fact]
    public async Task GivenAnOpenDialog_WhenCancelRedirectCalled_ThenActiveRedirectInfoIsCleared()
    {
        // Arrange
        var vm = await LoadedWith(GoldEntry());
        var fleet = new[] { EnRouteRepOnSilver() };
        _eligibility.Setup(e => e.FindEligibleRedirect(It.IsAny<ActiveRequestEntry>(), fleet))
            .Returns(InfoFor(GoldRequestId));
        vm.UpdateFleetData(fleet);
        vm.ShowRedirectDialog(GoldRequestId);

        // Act
        vm.CancelRedirect();

        // Assert
        Assert.Null(vm.ActiveRedirectInfo);
    }

    // ---- AC-3: optimistic confirm + success -------------------------------------------------------------

    private async Task<DispatcherRequestQueueViewModel> OpenDialogFor(Guid requestId)
    {
        var vm = await LoadedWith(GoldEntry());
        var fleet = new[] { EnRouteRepOnSilver() };
        _eligibility.Setup(e => e.FindEligibleRedirect(It.IsAny<ActiveRequestEntry>(), fleet))
            .Returns(InfoFor(requestId));
        vm.UpdateFleetData(fleet);
        vm.ShowRedirectDialog(requestId);
        return vm;
    }

    [Fact]
    public async Task GivenOpenRedirectDialog_WhenConfirmRedirectCalled_ThenActiveRedirectInfoClearedBeforeApiResponds()
    {
        // Arrange — the redirect API is held pending so the optimistic transition can be observed mid-flight.
        var vm = await OpenDialogFor(GoldRequestId);
        var gate = new TaskCompletionSource<RedirectRepResultDto>();
        _redirectService.Setup(s => s.RedirectAsync(RepId, GoldRequestId)).Returns(gate.Task);

        // Act — do NOT await to completion: capture state while the API call is still in flight.
        var inFlight = vm.ConfirmRedirectAsync();

        // Assert — the dialog is dismissed optimistically before the server responds.
        Assert.Null(vm.ActiveRedirectInfo);

        gate.SetResult(new RedirectRepResultDto(RepId, Guid.NewGuid(), GoldRequestId, "EnRoute"));
        await inFlight;
    }

    [Fact]
    public async Task GivenOpenRedirectDialog_WhenConfirmRedirectCalled_ThenIsRedirectingTrueBeforeApiResponds()
    {
        // Arrange
        var vm = await OpenDialogFor(GoldRequestId);
        var gate = new TaskCompletionSource<RedirectRepResultDto>();
        _redirectService.Setup(s => s.RedirectAsync(RepId, GoldRequestId)).Returns(gate.Task);

        // Act
        var inFlight = vm.ConfirmRedirectAsync();

        // Assert — IsRedirecting flips true optimistically, before the round-trip resolves.
        Assert.True(vm.IsRedirecting);

        gate.SetResult(new RedirectRepResultDto(RepId, Guid.NewGuid(), GoldRequestId, "EnRoute"));
        await inFlight;
    }

    [Fact]
    public async Task GivenOpenRedirectDialog_WhenConfirmRedirectCalled_ThenRedirectServiceCalledWithCorrectIds()
    {
        // Arrange
        var vm = await OpenDialogFor(GoldRequestId);
        _redirectService.Setup(s => s.RedirectAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new RedirectRepResultDto(RepId, Guid.NewGuid(), GoldRequestId, "EnRoute"));

        // Act
        await vm.ConfirmRedirectAsync();

        // Assert — the POST body carries the rep id and the target request id from the dialog's RedirectInfo.
        _redirectService.Verify(s => s.RedirectAsync(RepId, GoldRequestId), Times.Once);
    }

    [Fact]
    public async Task GivenInFlightRedirect_WhenApiSucceeds_ThenIsRedirectingFalse()
    {
        // Arrange
        var vm = await OpenDialogFor(GoldRequestId);
        _redirectService.Setup(s => s.RedirectAsync(RepId, GoldRequestId))
            .ReturnsAsync(new RedirectRepResultDto(RepId, Guid.NewGuid(), GoldRequestId, "EnRoute"));

        // Act
        await vm.ConfirmRedirectAsync();

        // Assert
        Assert.False(vm.IsRedirecting);
    }

    // ---- AC-4: error on non-2xx ------------------------------------------------------------------------

    [Fact]
    public async Task GivenConfirmRedirect_WhenApiReturnsNon2xx_ThenRedirectErrorMessageSet()
    {
        // Arrange — a non-2xx surfaces as a thrown exception from the redirect service.
        var vm = await OpenDialogFor(GoldRequestId);
        _redirectService.Setup(s => s.RedirectAsync(RepId, GoldRequestId))
            .ThrowsAsync(new HttpRequestException("Rep is no longer redirectable."));

        // Act
        await vm.ConfirmRedirectAsync();

        // Assert
        Assert.False(vm.IsRedirecting);
        Assert.False(string.IsNullOrEmpty(vm.RedirectError));
    }

    [Fact]
    public async Task GivenConfirmRedirect_WhenApiErrors_ThenTheDialogReappearsCarryingTheError()
    {
        // Arrange — the dialog is dismissed optimistically on confirm; on error it must re-surface so the error
        // banner (rendered inside the dialog per the composition map) is visible to the dispatcher.
        var vm = await OpenDialogFor(GoldRequestId);
        _redirectService.Setup(s => s.RedirectAsync(RepId, GoldRequestId))
            .ThrowsAsync(new HttpRequestException("Rep is no longer redirectable."));

        // Act
        await vm.ConfirmRedirectAsync();

        // Assert — the dialog is shown again with the same target and the error message set.
        Assert.NotNull(vm.ActiveRedirectInfo);
        Assert.Equal(GoldRequestId, vm.ActiveRedirectInfo!.ToRequestId);
        Assert.Equal("Rep is no longer redirectable.", vm.RedirectError);
    }

    [Fact]
    public async Task GivenConfirmRedirect_WhenApiReturnsError_ThenGetRedirectInfoReturnsNullForThatEntry()
    {
        // Arrange
        var vm = await OpenDialogFor(GoldRequestId);
        _redirectService.Setup(s => s.RedirectAsync(RepId, GoldRequestId))
            .ThrowsAsync(new HttpRequestException("Rep is no longer redirectable."));

        // Act
        await vm.ConfirmRedirectAsync();

        // Assert — eligibility for that entry is cleared, so the card's Redirect button stays disabled.
        Assert.Null(vm.GetRedirectInfo(GoldRequestId));
    }

    // ---- ADR-0011 captured-payload wire-contract proofs -------------------------------------------------

    private static T DeserializeWire<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void GivenARedirectRepResultJson_WhenDeserialised_ThenAllFieldsBindCorrectly()
    {
        // Arrange — the real POST /dispatcher/redirect 200 wire shape (camelCase), distinct value per field so a
        // field-name drift cannot pass coincidentally.
        const string json =
            """
            {
                "repId": "50000000-0000-0000-0000-000000000001",
                "fromRequestId": "bbbbbbbb-0000-0000-0000-000000000002",
                "toRequestId": "aaaaaaaa-0000-0000-0000-000000000009",
                "repState": "EnRoute"
            }
            """;

        // Act
        var result = DeserializeWire<RedirectRepResultDto>(json);

        // Assert
        Assert.Equal(Guid.Parse("50000000-0000-0000-0000-000000000001"), result.RepId);
        Assert.Equal(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), result.FromRequestId);
        Assert.Equal(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"), result.ToRequestId);
        Assert.Equal("EnRoute", result.RepState);
    }

    [Fact]
    public void GivenDispatcherFleetEntryJsonWithRedirectCooldownExpiresAt_WhenDeserialised_ThenAllFieldsBoundCorrectly()
    {
        // Arrange — the real GET /dispatcher/fleet entry with the BE-033 redirectCooldownExpiresAt field, a
        // distinct value per field including the new cooldown timestamp, through the same Web-defaults path.
        const string json =
            """
            {
                "repId": "50000000-0000-0000-0000-000000000001",
                "name": "J. Tran",
                "state": "EnRoute",
                "vehicleId": "30000000-0000-0000-0000-000000000007",
                "registration": "IA-4471",
                "lastPosition": { "lat": 41.8781, "lng": -93.0977 },
                "activeRequestId": "aaaaaaaa-0000-0000-0000-000000000009",
                "activeRequestTier": "Silver",
                "activeRequestTitle": "Hydraulic Pressure Loss",
                "humanControlled": true,
                "redirectCooldownExpiresAt": "2026-07-25T12:34:56+00:00"
            }
            """;

        // Act
        var dto = DeserializeWire<DispatcherFleetEntryDto>(json);

        // Assert — the new field binds to the expected DateTimeOffset, and it flows through the mapping.
        Assert.Equal(
            new DateTimeOffset(2026, 7, 25, 12, 34, 56, TimeSpan.Zero), dto.RedirectCooldownExpiresAt);
        Assert.Equal(
            new DateTimeOffset(2026, 7, 25, 12, 34, 56, TimeSpan.Zero),
            dto.ToFleetVehicleEntry().RedirectCooldownExpiresAt);
    }
}
