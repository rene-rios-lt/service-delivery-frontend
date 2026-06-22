namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Open/Closed seam invoked by the logout sequence BEFORE the JWT is cleared. The default
/// <c>NoOpLogoutSideEffect</c> completes immediately; FE-023 supplies the heartbeat-stop /
/// off-duty implementation by registering a real implementation at the composition root.
/// </summary>
public interface ILogoutSideEffect
{
    Task RunBeforeTokenClearedAsync();
}
