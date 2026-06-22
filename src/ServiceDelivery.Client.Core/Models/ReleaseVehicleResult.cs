namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Typed outcome of an <c>IReleaseVehicleAction</c> invocation. The null-object default returns
/// <see cref="NothingToRelease"/> so it can honour the contract without throwing (Liskov).
/// FE-014 supplies the real action that returns <see cref="Released"/> or <see cref="Blocked"/>.
/// </summary>
public enum ReleaseVehicleResult
{
    NothingToRelease,
    Released,
    Blocked
}
