using MudBlazor;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.ServiceRep.Components;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Host-generic <see cref="IReleaseConfirmation"/> that shows the <see cref="ReleaseConfirmationDialog"/>
/// (FE-014/AC-3) via MudBlazor's <see cref="IDialogService"/> and reduces the dialog outcome to the
/// bool the release action consumes: <c>true</c> when the rep taps Release, <c>false</c> when they
/// cancel or dismiss. The MudBlazor dialog dependency lives here so <c>ReleaseVehicleAction</c> stays
/// free of UI-framework types and unit-testable.
/// </summary>
public class MudReleaseConfirmation : IReleaseConfirmation
{
    private readonly IDialogService _dialogService;

    public MudReleaseConfirmation(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task<bool> ConfirmAsync(string registration)
    {
        var parameters = new DialogParameters<ReleaseConfirmationDialog>
        {
            { d => d.Registration, registration }
        };

        var dialog = await _dialogService.ShowAsync<ReleaseConfirmationDialog>("Release vehicle", parameters);
        var result = await dialog.Result;

        return result is { Canceled: false, Data: true };
    }
}
