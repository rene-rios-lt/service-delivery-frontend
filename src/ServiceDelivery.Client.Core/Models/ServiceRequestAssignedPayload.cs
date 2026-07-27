namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Wire-format DTO for the DispatchHub <c>ServiceRequestAssigned</c> event (FE-004). Its property names and
/// types mirror the backend <c>ServiceRequestAssignedPayload</c> EXACTLY — <c>RequestId</c>, <c>RepId</c>,
/// <c>RepName</c>, and <c>Eta</c> (a <c>double</c>) — so System.Text.Json binds every field over SignalR.
/// The ViewModel uses <c>RepName</c> to fill the assigned-rep line and flips the card's status to "Assigned".
/// </summary>
public record ServiceRequestAssignedPayload(
    Guid RequestId,
    Guid RepId,
    string RepName,
    double Eta);
