using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

public interface IPersonaNavigator
{
    void NavigateToPersonaHome(UserRole role);

    void NavigateToLogin();

    // The ServiceRep persona home is now the take-over screen (the first screen after login,
    // FE-007/AC-1), so a successful take-over needs a distinct destination: the idle rep view.
    void NavigateToRepIdleView();

    // When a JobOfferReceived event arrives on RepHub, the idle / waiting-for-offers view
    // (FE-020/AC-3) immediately presents the job offer (FE-008). The payload is carried so the
    // offer screen can render the offer without a re-fetch.
    void NavigateToJobOffer(JobOfferPayload offer);

    // After a successful accept (FE-009/AC-2), the rep transitions to the active-job view (FE-011),
    // where they navigate to the requester. The destination route is reserved now; FE-011 builds the page.
    void NavigateToActiveJob();
}
