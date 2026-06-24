using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Client for the SignalR RepHub. Narrow per the rep views' needs: start and stop the connection,
/// register a callback for the <c>JobOfferReceived</c> event (FE-020/AC-3, AC-5), and register a
/// callback for the <c>RedirectReceived</c> event (FE-011/AC-6) so the active-job view can update its
/// destination in-place. Connection lifecycle and transport details live in the host-shared
/// implementation, never in the ViewModel.
/// </summary>
public interface IRepHubService
{
    Task StartAsync();

    Task StopAsync();

    void OnJobOfferReceived(Func<JobOfferPayload, Task> handler);

    void OnRedirectReceived(Func<RedirectPayload, Task> handler);
}
