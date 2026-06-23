namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Typed outcome of an <c>IDeclineOfferService.DeclineAsync</c> call. Keeps HTTP status codes inside
/// the service implementation so the ViewModel reacts to a domain outcome, not a 200/409:
/// <see cref="Success"/> when the decline succeeded; <see cref="Conflict"/> when the offer expired
/// between the tap and the API call (backend 409). Both outcomes return the rep to the idle view
/// (FE-010/AC-2, AC-3).
/// </summary>
public enum DeclineOfferResult
{
    Success,
    Conflict
}
