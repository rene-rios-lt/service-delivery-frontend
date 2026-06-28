namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// The outcome of <c>MapsLoader.LoadAsync()</c> (FE-025). <see cref="IsAvailable"/> is <c>true</c> when a
/// non-blank API key was present and the SDK script was injected; <c>false</c> when the key was missing or
/// blank, in which case <see cref="DiagnosticMessage"/> explains why. FE-024's <c>GoogleMap</c> component
/// consumes this to decide whether to render the real map or the "map unavailable" placeholder, without
/// coupling to the loader's internals.
/// </summary>
public record MapsAvailability(bool IsAvailable, string? DiagnosticMessage);
