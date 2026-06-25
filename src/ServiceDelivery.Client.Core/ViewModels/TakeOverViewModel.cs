using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the rep take-over screen: loads idle vehicles, tracks the selected one, claims it
/// via <see cref="IVehicleService"/>, surfaces a conflict message, and navigates to the idle rep
/// view on success. Depends only on Core abstractions.
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

    public const string ConflictMessage =
        "That vehicle is no longer available. Please pick another.";

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

        LastResult = await _vehicleService.TakeOverAsync(vehicleId);

        if (LastResult == TakeOverResult.Success)
        {
            var selected = IdleVehicles.First(v => v.VehicleId == vehicleId);
            _claimedVehicleStore.SetVehicle(new ClaimedVehicle(
                selected.VehicleId, selected.Registration, selected.Model, selected.EquipmentTypes));
            _navigator.NavigateToRepIdleView();
            return;
        }

        ErrorMessage = ConflictMessage;
        SelectedVehicleId = null;
        await LoadAsync();
    }
}
