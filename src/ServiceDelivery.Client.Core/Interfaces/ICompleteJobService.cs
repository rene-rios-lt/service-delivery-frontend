namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Narrow client for the rep "Mark Complete" action (FE-013). <see cref="CompleteAsync"/> maps to
/// <c>POST /rep/complete</c> (BE-020), closing the active job and transitioning the rep back to
/// Available. HTTP details live in the implementation (<c>HttpCompleteJobService</c>), never in the
/// ViewModel. One operation, one caller (<c>ActiveJobViewModel</c>) — kept separate from
/// <c>IArriveService</c> so neither depends on the other's methods (ISP).
/// </summary>
public interface ICompleteJobService
{
    Task CompleteAsync();
}
