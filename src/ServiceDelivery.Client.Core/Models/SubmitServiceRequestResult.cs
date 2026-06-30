namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Typed outcome of an <c>IServiceRequestService.SubmitAsync</c> call (FE-015 AC-4/AC-5). Keeps HTTP
/// status codes inside the service implementation so the ViewModel reacts to a domain outcome, not a
/// 200/500: <see cref="Success"/> when the request was accepted (carrying the new request id for the
/// pending view), <see cref="Error"/> when the API rejected it (carrying a message for the inline band).
/// A closed type hierarchy — the only two outcomes the submit form distinguishes.
/// </summary>
public abstract record SubmitServiceRequestResult
{
    private SubmitServiceRequestResult()
    {
    }

    public sealed record Success(Guid RequestId) : SubmitServiceRequestResult;

    public sealed record Error(string Message) : SubmitServiceRequestResult;
}
