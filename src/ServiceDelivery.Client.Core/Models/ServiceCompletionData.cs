namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Client-side view record for the completion screen (FE-019). Assembled from TWO client sources — the rep
/// name from <c>RequesterTrackingViewModel</c> state (last <c>RepAssigned</c> push) and the DTC title
/// captured by <c>SubmitRequestViewModel</c> at submit success — and deposited into
/// <c>IServiceCompletedStore</c> by the tracking VM when the <c>ServiceCompleted</c> event fires. It is
/// NEVER on the wire: the wire payload (<see cref="ServiceCompletedPayload"/>) carries only the request id;
/// this record carries the display data the completion subtitle needs.
/// </summary>
public record ServiceCompletionData(string RepName, string DtcTitle);
