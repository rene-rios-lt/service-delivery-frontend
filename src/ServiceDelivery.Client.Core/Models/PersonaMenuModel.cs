using System.Collections.Generic;

namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// Immutable shape of a persona's menu: the header title (persona name), the role/context line,
/// an optional vehicle-context line (null when no vehicle is claimed — FE-007/FE-014 supply it),
/// and the ordered list of menu entries.
/// </summary>
public record PersonaMenuModel(
    string Title,
    string ContextLine,
    string? VehicleContext,
    IReadOnlyList<PersonaMenuItem> Items);
