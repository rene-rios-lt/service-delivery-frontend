using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Services;

/// <summary>
/// Scoped, in-memory implementation of <see cref="IServiceCompletedStore"/> (FE-019). Holds the DTC title
/// deposited by <c>SubmitRequestViewModel</c> at submit success and the <see cref="ServiceCompletionData"/>
/// deposited by <c>RequesterTrackingViewModel</c> when the <c>ServiceCompleted</c> event fires, both within
/// one session scope. Both are nullable so <c>RequesterCompleteViewModel</c> can degrade gracefully on a
/// refresh / deep-link. Registered in every host bootstrapper so the completion ViewModel can always
/// resolve it. Mirrors <c>InMemoryRepAssignedStore</c>.
/// </summary>
public class InMemoryServiceCompletedStore : IServiceCompletedStore
{
    public ServiceCompletionData? CurrentPayload { get; private set; }

    public string? DtcTitle { get; private set; }

    public void SetDtcTitle(string title) => DtcTitle = title;

    public void SetPayload(ServiceCompletionData payload) => CurrentPayload = payload;

    public void ClearPayload() => CurrentPayload = null;
}
