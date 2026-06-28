namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Abstraction over the "Release vehicle?" confirmation prompt (FE-014/AC-3), keeping the dialog
/// technology (MudBlazor's <c>IDialogService</c>) out of the release orchestration so the action stays
/// unit-testable. Returns <c>true</c> when the rep confirms, <c>false</c> when they cancel or dismiss.
/// Mirrors the <c>ILogoutSideEffect</c> seam pattern: Core declares the capability, a host-generic UI
/// service implements it over the real dialog.
/// </summary>
public interface IReleaseConfirmation
{
    Task<bool> ConfirmAsync(string registration);
}
