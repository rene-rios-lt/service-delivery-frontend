namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// On-duty heartbeat loop for the ServiceRep persona (FE-023). While a vehicle is claimed
/// (<see cref="IClaimedVehicleStore.CurrentVehicle"/> is non-null) the loop sends
/// <c>POST /rep/heartbeat</c> on a fixed interval so the backend keeps the rep marked
/// human-controlled. The loop spans all rep pages (idle → offer → job) and is therefore an
/// on-duty-long concern, decoupled from any single page's lifecycle. Started when the rep enters
/// the idle view (post take-over) and stopped on explicit logout; it also self-terminates within
/// one interval once the claimed-vehicle store is cleared (release path).
/// </summary>
public interface IHeartbeatService
{
    /// <summary>Starts the heartbeat loop. Idempotent — no-op if already running.</summary>
    Task StartAsync();

    /// <summary>Stops the heartbeat loop immediately. Idempotent — no-op if not running.</summary>
    Task StopAsync();

    /// <summary>True while the loop is active and sending heartbeats.</summary>
    bool IsRunning { get; }
}
