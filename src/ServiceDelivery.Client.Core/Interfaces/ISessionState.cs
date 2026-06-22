namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Proactive, claim-based expiry check over the stored token. Lets callers (app start,
/// route guards, pre-request checks) detect expiry before issuing a request.
/// </summary>
public interface ISessionState
{
    /// <summary>
    /// True if there is no stored token, or the stored token's <c>exp</c> claim is in the past.
    /// </summary>
    Task<bool> IsTokenExpiredAsync();
}
