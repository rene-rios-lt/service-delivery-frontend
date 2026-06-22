namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// A single entry in the persona menu. <see cref="ActionKey"/> identifies the behaviour the shell
/// wires to the entry (e.g. a navigation route or a well-known action such as "logout" / "release").
/// </summary>
public record PersonaMenuItem(
    string Label,
    string Icon,
    string ActionKey,
    bool IsDestructive = false,
    bool IsEnabled = true);
