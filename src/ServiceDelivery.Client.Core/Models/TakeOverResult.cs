namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Typed outcome of an <c>IVehicleService.TakeOverAsync</c> call. Keeps HTTP status codes
/// inside the service implementation so ViewModels react to a domain outcome, not a 200/409:
/// <see cref="Success"/> when the take-over claim succeeded; <see cref="Conflict"/> when the
/// vehicle is no longer available (backend 409).
/// </summary>
public enum TakeOverResult
{
    Success,
    Conflict
}
