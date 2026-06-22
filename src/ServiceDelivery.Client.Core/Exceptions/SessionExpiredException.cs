namespace ServiceDelivery.Client.Core.Exceptions;

/// <summary>
/// Thrown when an in-flight request is rejected with a 401, after the expiry action has already
/// run (token cleared, redirect to login issued). Carries no token. Propagating this exception
/// instead of returning the 401 body unwinds the calling action's success continuation, so a
/// pending UI mutation never runs against an invalid token.
/// </summary>
public class SessionExpiredException : Exception
{
    public SessionExpiredException()
        : base("The session has expired; the in-flight request was aborted.")
    {
    }
}
