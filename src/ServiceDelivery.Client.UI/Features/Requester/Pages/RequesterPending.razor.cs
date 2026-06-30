using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.UI.Features.Requester.Pages;

/// <summary>
/// Code-behind for <see cref="RequesterPending"/> (FE-016). Owns the page's interaction glue only: sets
/// the app-bar chrome on init, starts the RequesterHub via the ViewModel (swallowing any residual connect
/// failure as a final safety net, BUG-038), and stops the hub on dispose. All state and decisions —
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

    public async ValueTask DisposeAsync()
    {
        // Restore the default app-bar chrome so the next route is unaffected by this screen's overrides.
        Shell.SetTitle(null);
        await ViewModel.StopAsync();
    }
}
