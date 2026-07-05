using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the rep take-over screen: loads idle vehicles, tracks the selected one, and claims it
/// via <see cref="IVehicleService"/>. On a 409 conflict (the vehicle was claimed out from under the rep)
/// it auto-retries the next available candidate — re-fetching the idle list and attempting each
/// un-attempted vehicle until one succeeds or the list is exhausted (BUG-045). On success it navigates to
/// the idle rep view; on exhaustion it surfaces <see cref="ExhaustedMessage"/>. The retry is bounded by the
/// finite set of vehicles attempted this invocation, so it can never spin. Depends only on Core abstractions.
/// </summary>
public class TakeOverViewModel
{
    private readonly IVehicleService _vehicleService;
    private readonly IPersonaNavigator _navigator;
    private readonly IClaimedVehicleStore _claimedVehicleStore;

    public TakeOverViewModel(
        IVehicleService vehicleService,
        IPersonaNavigator navigator,
        IClaimedVehicleStore claimedVehicleStore)
    {
        _vehicleService = vehicleService;
        _navigator = navigator;
        _claimedVehicleStore = claimedVehicleStore;
    }

    public IReadOnlyList<IdleVehicle> IdleVehicles { get; private set; } = [];

    public Guid? SelectedVehicleId { get; private set; }

    public TakeOverResult? LastResult { get; private set; }

    public bool IsBusy { get; private set; }

    public string? ErrorMessage { get; private set; }

    public const string ExhaustedMessage =
        "The vehicle you picked was just taken and no others are available right now. Please try again shortly.";

    public bool IsEligible { get; private set; } = true;

    public const string IneligibleMessage =
        "You're already on a job, so you can't take over another vehicle right now.";

    public bool CanTakeOver => IsEligible && SelectedVehicleId is not null;

    public void SetEligibility(bool repIsIdle)
    {
        IsEligible = repIsIdle;
    }

    public string? SelectedRegistration =>
        IdleVehicles.FirstOrDefault(v => v.VehicleId == SelectedVehicleId)?.Registration;

    public async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            IdleVehicles = await _vehicleService.GetIdleVehiclesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Select(Guid vehicleId)
    {
        SelectedVehicleId = vehicleId;
    }

    public async Task TakeOverAsync()
    {
        if (!CanTakeOver || SelectedVehicleId is not { } vehicleId)
            return;

        var attempted = new HashSet<Guid>();
        var candidateId = vehicleId;

        while (true)
        {
            attempted.Add(candidateId);
            LastResult = await _vehicleService.TakeOverAsync(candidateId);

            if (LastResult == TakeOverResult.Success)
            {
                ClaimAndNavigate(candidateId);
                return;
            }

            // Conflict: the candidate was claimed out from under the rep. Refresh the list and try
            // the next vehicle we have not yet attempted. The attempted set bounds the loop — once no
            // un-attempted candidate remains, we exhaust rather than spin (BUG-045).
            await LoadAsync();

            var nextCandidate = IdleVehicles.FirstOrDefault(v => !attempted.Contains(v.VehicleId));
            if (nextCandidate is null)
            {
                ErrorMessage = ExhaustedMessage;
                SelectedVehicleId = null;
                return;
            }

            candidateId = nextCandidate.VehicleId;
            SelectedVehicleId = candidateId;
        }
    }

    private void ClaimAndNavigate(Guid vehicleId)
    {
        var selected = IdleVehicles.First(v => v.VehicleId == vehicleId);
        _claimedVehicleStore.SetVehicle(new ClaimedVehicle(
            selected.VehicleId, selected.Registration, selected.Model, selected.EquipmentTypes));
        _navigator.NavigateToRepIdleView();
    }
}
