using ServiceDelivery.Client.Appium.Helpers;

namespace ServiceDelivery.Client.Appium;

/// <summary>
/// QUAL-009 — live end-to-end coverage of FE-023's heartbeat / clean go-off-duty behaviour, driven
/// through the real Mobile app. The heartbeat is a background concern with no UI surface (FE-023
/// AC-5: no dedicated screen), so these scenarios drive the app (take over, stay, close) and then
/// assert the resulting <b>backend</b> state via <see cref="BackendApiHelper"/> — the only observable
/// proof that the heartbeat is (or is not) keeping the rep human-controlled.
///
/// <para>
/// <b>Test-scoped timing.</b> <c>scripts/local/test-appium.sh</c> shortens the backend's
/// human-controlled heartbeat timeout to 25 s (<c>HeartbeatTimeout__TimeoutSeconds=25</c>), above the
/// app's 15 s heartbeat interval so an actively-beating rep is never falsely swept, but short enough
/// that the timeout path resolves within a test. The default (demo/prod) timeout stays 45 s.
/// </para>
///
/// <para>
/// <b>Scope.</b> This run is backend-only (<c>SD_SKIP_SIMULATOR=1</c>), so the SIM-009 "the simulator
/// does not re-assume a yielded rep" half of FE-023 AC-4 is <i>not</i> exercised here — there is no
/// simulator to re-assume. That half is covered by the simulator-inclusive live verification recorded
/// against QUAL-009 (and by SIM-009's own tests). These scenarios cover the frontend-observable
/// obligations: the app keeps the rep on duty while running, and stops doing so when it is closed.
/// </para>
/// </summary>
[TestFixture]
public sealed class HeartbeatGoOffDutyTests : AppiumTestBase
{
    /// <summary>Backend sweep window after the app stops beating: 25 s timeout + poll + EF slack.</summary>
    private static readonly TimeSpan SweepWindow = TimeSpan.FromSeconds(45);

    /// <summary>Idle dwell that outlasts the 25 s timeout, proving the app's heartbeat keeps firing.</summary>
    private static readonly TimeSpan PastTimeoutDwell = TimeSpan.FromSeconds(33);

    [Test]
    public void GivenRepOnDuty_WhenIdleLongerThanTheHeartbeatTimeout_ThenBackendKeepsTheRepOnDuty()
    {
        // Arrange — take over a vehicle through the app; rep1 is now on duty and the FE-023 heartbeat
        // is running in the background of the idle view.
        TakeOverFirstIdleVehicle();
        var baseUrl = AppiumConfig.BackendBaseUrl;

        var afterTakeOver = BackendApiHelper.GetRep1FleetState(baseUrl);
        Assert.That(afterTakeOver, Is.Not.Null, "rep1 should appear in the dispatcher fleet after take-over");
        Assert.That(afterTakeOver!.HumanControlled, Is.True, "take-over should mark rep1 human-controlled");

        // Act — stay on the idle screen well past the (test-shortened) 25 s timeout. The app's 15 s
        // heartbeat must keep firing; without it the backend sweep would mark rep1 Offline by ~25 s.
        Thread.Sleep(PastTimeoutDwell);

        // Assert — the backend still sees rep1 on duty: the heartbeat kept the human in control.
        var afterDwell = BackendApiHelper.GetRep1FleetState(baseUrl);
        Assert.That(afterDwell, Is.Not.Null, "rep1 should still be in the fleet");
        Assert.That(afterDwell!.State, Is.Not.EqualTo("Offline"),
            "rep1 should still be on duty after the timeout window — the app heartbeat should have prevented a sweep");
        Assert.That(afterDwell.HumanControlled, Is.True,
            "rep1 should still be human-controlled — heartbeats keep the human in control across the idle dwell");
    }

    [Test]
    public void GivenRepOnDuty_WhenAppIsClosedSoHeartbeatsStop_ThenBackendTimesOutAndVehicleReappears()
    {
        // Arrange — take over a vehicle and capture which one, so we can assert it returns to the fleet.
        TakeOverFirstIdleVehicle();
        var baseUrl = AppiumConfig.BackendBaseUrl;

        var afterTakeOver = BackendApiHelper.GetRep1FleetState(baseUrl);
        Assert.That(afterTakeOver, Is.Not.Null, "rep1 should appear in the dispatcher fleet after take-over");
        Assert.That(afterTakeOver!.HumanControlled, Is.True, "take-over should mark rep1 human-controlled");
        var vehicleId = afterTakeOver.VehicleId;

        // Act — close the app so the heartbeat stops abruptly (the "app closed / backgrounded" path of
        // AC-2). The backend's stale-heartbeat sweep should then time the rep out.
        CloseAppUnderTest();

        // Assert — within the timeout + sweep window the backend sweeps rep1 Offline (human-control
        // cleared) and the parked vehicle reappears in the take-over list for any idle rep.
        var timedOutAndParked = BackendApiHelper.WaitUntilOffDutyAndTakeable(baseUrl, vehicleId, SweepWindow);
        Assert.That(timedOutAndParked, Is.True,
            "after the app closed and heartbeats stopped, the backend should have timed rep1 out, " +
            "parked the vehicle, and returned it to the take-over list");
    }
}
