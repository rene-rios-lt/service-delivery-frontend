using Microsoft.AspNetCore.Components;
using ServiceDelivery.Client.Core.ViewModels;

namespace ServiceDelivery.Client.UI.Features.Requester.Pages;

/// <summary>
/// Code-behind for <see cref="RequesterComplete"/> (FE-019). Owns the page's interaction glue only: sets
/// the app-bar "Request closed" subtitle on init (and re-applies it in <c>OnAfterRenderAsync(firstRender)</c>
/// so an already-loaded shell picks it up on the initial render — the BUG-044 pattern the tracking page
/// uses), and restores the default chrome on dispose. The title is left at its default ("Service Delivery")
/// per the mockup. All state and decisions — the completion subtitle, the two navigation actions — live in
/// <see cref="RequesterCompleteViewModel"/>; this class holds no business logic (Single Responsibility).
/// </summary>
public partial class RequesterComplete : IDisposable
{
    // The app-bar subtitle for the completion screen (mockup: "Request closed" under the default
    // "Service Delivery" title).
    private const string CompleteSubtitle = "Request closed";

    [Inject] private RequesterCompleteViewModel ViewModel { get; set; } = default!;

    [Inject] private ShellViewModel Shell { get; set; } = default!;

    protected override void OnInitialized()
    {
        Shell.SetSubtitle(CompleteSubtitle);
    }

    // BUG-044 pattern: re-apply the subtitle in the post-render regime so the shared PersonaShell (which
    // persists across the tracking→complete navigation) picks up the TitleChanged nudge on the INITIAL
    // render of a running app. Guarded on firstRender so it runs once per entry.
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Shell.SetSubtitle(CompleteSubtitle);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Restore the default app-bar chrome so the next route is unaffected by this screen's override.
        Shell.SetSubtitle(null);
    }
}
