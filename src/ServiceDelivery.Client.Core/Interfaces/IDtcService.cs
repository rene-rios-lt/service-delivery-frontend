using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Reads the dealer's Diagnostic Trouble Codes from <c>GET /dtcs</c> (FE-015 AC-2) so the submit form
/// can populate its fault dropdown. A focused, read-only capability (Interface Segregation) — the
/// concrete HTTP implementation lives in a host-shared UI service and is injected via DI.
/// </summary>
public interface IDtcService
{
    Task<IReadOnlyList<DtcItem>> GetDtcsAsync();
}
