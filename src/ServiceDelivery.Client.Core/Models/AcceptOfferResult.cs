namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Typed outcome of an <c>IJobOfferService.AcceptAsync</c> call. Keeps HTTP status codes inside
/// the service implementation so the ViewModel reacts to a domain outcome, not a 200/409:
/// <see cref="Success"/> when the accept succeeded; <see cref="Conflict"/> when the offer expired
/// between the tap and the API call (backend 409).
/// </summary>
public enum AcceptOfferResult
{
    Success,
    Conflict
}
