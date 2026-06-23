using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Client for the SignalR RepHub. Narrow per the idle / waiting-for-offers view's needs
/// (FE-020/AC-3, AC-5): start and stop the connection, and register a single callback invoked
/// when a <c>JobOfferReceived</c> event arrives. Connection lifecycle and transport details live
/// in the host-shared implementation, never in the ViewModel.
/// </summary>
public interface IRepHubService
{
    Task StartAsync();

    Task StopAsync();

    void OnJobOfferReceived(Func<JobOfferPayload, Task> handler);
}
