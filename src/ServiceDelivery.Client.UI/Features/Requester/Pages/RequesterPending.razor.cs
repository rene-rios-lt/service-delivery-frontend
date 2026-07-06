using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.UI.Features.Requester.Pages;

/// <summary>
/// Code-behind for <see cref="RequesterPending"/> (FE-016). Owns the page's interaction glue only: sets
/// the app-bar chrome on init and starts the RequesterHub via the ViewModel (swallowing any residual connect
/// failure as a final safety net, BUG-038). It does NOT stop the hub on dispose — the shared, scoped
/// RequesterHub connection persists across the pending→tracking→complete navigations (FE-019) so no
/// post-navigation push is dropped; it is torn down when the DI scope ends. All state and decisions —
/// the requester's real tier, the RepAssigned auto-transition (AC-3), the hub connection state — live in
/// <see cref="RequesterPendingViewModel"/>; this class holds no business logic (Single Responsibility).
/// </summary>
public partial class RequesterPending : IAsyncDisposable
{
    [Inject] private RequesterPendingViewModel ViewModel { get; set; } = default!;

    [Inject] private ShellViewModel Shell { get; set; } = default!;

    [Inject] private ILogger<RequesterPending> Logger { get; set; } = default!;

    protected override void OnInitialized()
    {
        // App-bar chrome for the pending screen (matches the mockup): "Request Service" title with the
        // submitted DTC name as subtitle. The submit screen sets the same title; the subtitle defaults to
        // null here (no DTC carried into FE-016) so the shell keeps its derived line.
        Shell.SetTitle("Request Service");
    }

    protected override async Task OnInitializedAsync()
    {
        // BUG-038: final safety net. ViewModel.StartAsync already swallows hub-connect and profile-fetch
        // failures, but wrap the call here too so nothing reaching OnInitializedAsync can escape to
        // Blazor's #blazor-error-ui banner. A connect failure leaves the screen in its reconnecting state.
        try
        {
            await ViewModel.StartAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "RequesterHub start failed on pending-screen init; staying in reconnecting state.");
        }
    }

    public ValueTask DisposeAsync()
    {
        // Restore the default app-bar chrome so the next route is unaffected by this screen's overrides.
        Shell.SetTitle(null);

        // FE-019: do NOT stop the shared RequesterHub connection here. It is a scoped IAsyncDisposable that
        // must PERSIST across the pending→tracking→complete navigations so no post-navigation push (position
        // updates, the redirect pair, and especially the one-shot ServiceCompleted) is dropped — SignalR does
        // not buffer group messages for an absent client, and the connection is torn down when the DI scope
        // ends (session end), which is the correct "leaving the flow" point. Previously this stopped the
        // connection and the tracking view's re-StartAsync raced the async stop (HubConnection.StartAsync
        // threw "cannot be started if it is not in the Disconnected state", was swallowed into the BUG-038
        // back-off), opening the window where the ServiceCompleted push was lost.
        return ValueTask.CompletedTask;
    }
}
