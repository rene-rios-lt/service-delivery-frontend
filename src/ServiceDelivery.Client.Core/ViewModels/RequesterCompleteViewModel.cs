using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Orchestrates the requester "Your service is complete" screen (FE-019). Reads the assembled
/// <see cref="ServiceCompletionData"/> from <see cref="IServiceCompletedStore"/> and exposes the completion
/// subtitle with graceful degrade, plus the two terminal actions (submit a new request / done), both of
/// which return the requester to their persona home. Depends only on Core abstractions — the page binds to
/// this state and delegates every interaction here (Single Responsibility / Dependency Inversion).
/// </summary>
public class RequesterCompleteViewModel
{
    // AC-4 degraded form: shown when the store is unpopulated (refresh / deep-link) or either display field
    // is absent, so the screen never renders a half-built sentence (e.g. " resolved your ."). The static
    // heading "Your service is complete." lives in the Razor markup and is always shown regardless.
    private const string GenericSubtitle = "Your service is complete. Thanks for using Service Delivery.";

    private readonly IServiceCompletedStore _store;
    private readonly IPersonaNavigator _navigator;

    public RequesterCompleteViewModel(IServiceCompletedStore store, IPersonaNavigator navigator)
    {
        _store = store;
        _navigator = navigator;
    }

    // AC-1/AC-4: the completion subtitle. Full form ("{RepName} resolved your {DtcTitle}. Thanks for using
    // Service Delivery.") when both the rep name and DTC title are present; degrades to the generic
    // thank-you when the store is unpopulated or either field is empty.
    public string CompletionSubtitle
    {
        get
        {
            var payload = _store.CurrentPayload;
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.RepName)
                || string.IsNullOrWhiteSpace(payload.DtcTitle))
            {
                return GenericSubtitle;
            }

            return $"{payload.RepName} resolved your {payload.DtcTitle}. Thanks for using Service Delivery.";
        }
    }

    // AC-3: the primary "Submit a new request" action returns the requester to their persona home (the
    // submit form).
    public void SubmitNewRequest() => _navigator.NavigateToPersonaHome(UserRole.Requester);

    // AC-3: the "Done" action also returns the requester to their persona home.
    public void Dismiss() => _navigator.NavigateToPersonaHome(UserRole.Requester);
}
