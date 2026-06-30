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

    // After releasing the claimed vehicle at end of shift (FE-014/AC-4), the rep returns to the
    // take-over screen (FE-007) to pick another idle vehicle or hand the device back.
    void NavigateToTakeOver();

    // After a successful service-request submit (FE-015/AC-4), the requester transitions to the pending
    // / "finding your technician" view (FE-016 owns that route; this story navigates to its stub).
    void NavigateToRequesterPending();

    // When a RepAssigned event arrives on RequesterHub, the pending / "finding your technician" view
    // (FE-016/AC-3) transitions to the rep-tracking view (FE-017 owns that route; this story navigates
    // to its stub).
    void NavigateToRequesterTracking();
}
