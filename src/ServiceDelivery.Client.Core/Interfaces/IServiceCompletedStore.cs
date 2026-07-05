using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Cross-navigation hand-off store for the requester completion screen (FE-019), populated in two phases
/// from two ViewModels within one session scope:
/// <list type="bullet">
/// <item><see cref="SetDtcTitle"/> — called by <c>SubmitRequestViewModel</c> at submit success, threading
/// the selected fault title forward at the earliest point it is known.</item>
/// <item><see cref="SetPayload"/> — called by <c>RequesterTrackingViewModel</c> when the
/// <c>ServiceCompleted</c> event arrives, depositing the assembled <see cref="ServiceCompletionData"/>
/// (rep name from tracking state + the threaded DTC title).</item>
/// </list>
/// <c>RequesterCompleteViewModel</c> reads <see cref="CurrentPayload"/>. Both values are nullable so the
/// completion ViewModel degrades gracefully on a refresh / deep-link where the store is unpopulated.
/// Mirrors <see cref="IRepAssignedStore"/>.
/// </summary>
public interface IServiceCompletedStore
{
    ServiceCompletionData? CurrentPayload { get; }

    string? DtcTitle { get; }

    void SetDtcTitle(string title);

    void SetPayload(ServiceCompletionData payload);

    void ClearPayload();
}
