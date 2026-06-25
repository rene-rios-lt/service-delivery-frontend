namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Narrow client for the rep "I've Arrived" action (FE-012). <see cref="ArriveAsync"/> maps to
/// <c>POST /rep/arrive</c> (BE-019), transitioning the rep to the OnSite state on the backend. HTTP
/// details live in the implementation, never in the ViewModel. One operation, one caller
/// (<c>ActiveJobViewModel</c>) — kept separate from <c>IActiveJobService</c> so neither depends on
/// the other's methods (ISP).
/// </summary>
public interface IArriveService
{
    Task ArriveAsync();
}
