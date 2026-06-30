using Microsoft.AspNetCore.Components;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Maps.Components;

namespace ServiceDelivery.Client.UI.Features.Requester.Pages;

/// <summary>
/// Code-behind for <see cref="SubmitRequest"/> (FE-015). Owns the page's interaction glue only: sets the
/// app-bar chrome on init (and restores it on dispose), loads the DTC list, and delegates every gesture
/// (map tap, "use my location", DTC selection, submit) to <see cref="SubmitRequestViewModel"/>. All state
/// and decisions live in the ViewModel — this class holds no business logic (Single Responsibility).
/// </summary>
public partial class SubmitRequest : IDisposable
{
    // The map centres on Des Moines (the POC dealer's city) until the requester drops a pin; once a pin
    // is set the centre follows it (the tap capture records the location; no live pin overlay for the POC).
    private const double DefaultLat = 41.5868;
    private const double DefaultLng = -93.6250;

    private GoogleMap? _map;

    [Inject] private SubmitRequestViewModel ViewModel { get; set; } = default!;

    [Inject] private ShellViewModel Shell { get; set; } = default!;

    private double MapLat => ViewModel.SelectedLocation?.Lat ?? DefaultLat;

    private double MapLng => ViewModel.SelectedLocation?.Lng ?? DefaultLng;

    protected override void OnInitialized()
    {
        // App-bar chrome for the submit screen (matches the mockup): title + subtitle. Restored in Dispose.
        Shell.SetTitle("Request Service");
        Shell.SetSubtitle("Report an equipment fault");
    }

    protected override async Task OnInitializedAsync()
    {
        await ViewModel.LoadDtcsAsync();
    }

    // AC-1b: a map tap deposits the tapped coordinate as the selected location, then re-renders so the
    // pin-set label appears and the submit button re-evaluates its enabled state.
    private async Task OnMapClickedAsync(GpsPoint point)
    {
        ViewModel.SetLocation(point);
        await InvokeAsync(StateHasChanged);
    }

    // AC-1c: "Use my current location" reads device GPS via the ViewModel; a null result is a no-op.
    private async Task OnUseMyLocationAsync()
    {
        await ViewModel.UseMyLocationAsync();
    }

    // AC-2: the dropdown selection sets the DTC id; an empty value (the placeholder) is ignored.
    private void OnDtcChanged(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var dtcId))
        {
            ViewModel.SelectDtc(dtcId);
        }
    }

    // AC-4/AC-5: submit POSTs the request; the ViewModel navigates on success or sets the inline error.
    private async Task OnSubmitAsync()
    {
        await ViewModel.SubmitAsync();
    }

    public void Dispose()
    {
        // Restore the default app-bar chrome so the next route is unaffected by this screen's overrides.
        Shell.SetTitle(null);
        Shell.SetSubtitle(null);
    }
}
