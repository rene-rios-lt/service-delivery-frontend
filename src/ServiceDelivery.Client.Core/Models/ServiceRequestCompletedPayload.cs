namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-format DTO for the DispatchHub <c>ServiceRequestCompleted</c> event (FE-004). Carries only the
/// <c>RequestId</c> of the completed request, mirroring the backend <c>ServiceRequestCompletedPayload</c>
/// exactly so System.Text.Json binds it over SignalR. The ViewModel removes the matching card from the queue.
/// </summary>
public record ServiceRequestCompletedPayload(Guid RequestId);
