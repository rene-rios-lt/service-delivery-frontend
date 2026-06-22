namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// The single clear-token-then-redirect action invoked when a session is detected as expired,
/// from either detection path (proactive claim check or a reactive 401 response).
/// </summary>
public interface ISessionExpiryHandler
{
    /// <summary>
    /// Clears the stored token, then redirects to the login screen — in that order.
    /// </summary>
    Task HandleExpiredSessionAsync();
}
