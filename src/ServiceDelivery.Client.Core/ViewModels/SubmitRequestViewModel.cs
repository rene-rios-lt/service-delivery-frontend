using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the Requester submit-request form (FE-015). Holds the form state — the selected location
/// (from a map tap or device GPS), the selected DTC, the submit-enable gate, the loading flag, and any
/// inline error — and drives the four collaborators (DTC read, request submit, geolocation, navigation)
/// through Core abstractions only. The Razor page binds to this state and delegates every interaction
/// here, so the page stays display-only (Single Responsibility / Dependency Inversion).
/// </summary>
public class SubmitRequestViewModel
{
    private const string SubmitFailedMessage =
        "We couldn't submit your request. Please try again.";

    private readonly IDtcService _dtcService;
    private readonly IServiceRequestService _requestService;
    private readonly IGeolocationService _geolocation;
    private readonly IPersonaNavigator _navigator;

    public SubmitRequestViewModel(
        IDtcService dtcService,
        IServiceRequestService requestService,
        IGeolocationService geolocation,
        IPersonaNavigator navigator)
    {
        _dtcService = dtcService;
        _requestService = requestService;
        _geolocation = geolocation;
        _navigator = navigator;
    }

    public IReadOnlyList<DtcItem> Dtcs { get; private set; } = [];

    public GpsPoint? SelectedLocation { get; private set; }

    public Guid? SelectedDtcId { get; private set; }

    public bool IsLoading { get; private set; }

    public string? ErrorMessage { get; private set; }

    // AC-3: the primary action is enabled only when BOTH a location and a DTC are set.
    public bool IsSubmitEnabled => SelectedLocation is not null && SelectedDtcId is not null;

    // AC-2: populate the fault dropdown from GET /dtcs.
    public async Task LoadDtcsAsync()
    {
        Dtcs = await _dtcService.GetDtcsAsync();
    }

    // AC-1b: a map tap deposits the tapped coordinate as the selected location.
    public void SetLocation(GpsPoint point)
    {
        SelectedLocation = point;
    }

    // AC-2: the dropdown selection sets the DTC id posted on submit.
    public void SelectDtc(Guid dtcId)
    {
        SelectedDtcId = dtcId;
    }

    // AC-1c: "Use my current location" reads the device GPS. A null result (permission denied /
    // hardware unavailable) leaves the location unset — no crash, the user can still drop a pin.
    public async Task UseMyLocationAsync()
    {
        var position = await _geolocation.GetCurrentLocationAsync();
        if (position is not null)
        {
            SelectedLocation = position;
        }
    }

    // AC-4 / AC-5: submit POSTs the request, then navigates to the pending view on success or surfaces
    // an inline error on failure (the form stays). A no-op when the form is incomplete (the button is
    // disabled, but the path is guarded too).
    public async Task SubmitAsync()
    {
        if (!IsSubmitEnabled || SelectedLocation is not { } location || SelectedDtcId is not { } dtcId)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _requestService.SubmitAsync(location.Lat, location.Lng, dtcId);

            switch (result)
            {
                case SubmitServiceRequestResult.Success:
                    _navigator.NavigateToRequesterPending();
                    break;
                case SubmitServiceRequestResult.Error error:
                    ErrorMessage = string.IsNullOrWhiteSpace(error.Message) ? SubmitFailedMessage : error.Message;
                    break;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
