using System;
using System.Collections.Generic;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

/// <summary>
/// Builds the per-persona <see cref="PersonaMenuModel"/> for a <see cref="UserProfile"/>. This is the
/// single place per-persona menu composition changes — adding a new item or a new persona's item set
/// happens here (Open/Closed for the shell components, which stay data-driven).
/// </summary>
public class PersonaMenuFactory
{
    public const string LogoutActionKey = "logout";
    public const string ReleaseActionKey = "release";

    /// <summary>
    /// Builds the persona menu. The factory stays pure — it always builds the "Release vehicle" item
    /// enabled and reaches for no state itself. The InProgress gate (FE-014/AC-2) is applied to the
    /// loaded menu by the shell via <c>ShellViewModel.SetReleaseEnabled</c>, driven by the active-job
    /// screen — the single orchestrator of release-enabled state.
    /// </summary>
    public PersonaMenuModel Build(UserProfile profile)
    {
        var contextLine = ContextLineFor(profile.Role);
        var items = ItemsFor(profile.Role);

        return new PersonaMenuModel(
            Title: profile.Name,
            ContextLine: contextLine,
            VehicleContext: null,
            Items: items);
    }

    private static string ContextLineFor(UserRole role) => role switch
    {
        UserRole.ServiceRep => "Service Rep",
        UserRole.Dispatcher => "Dispatcher",
        UserRole.Requester => "Requester",
        _ => string.Empty
    };

    private static IReadOnlyList<PersonaMenuItem> ItemsFor(UserRole role) => role switch
    {
        UserRole.ServiceRep => new List<PersonaMenuItem>
        {
            new("Waiting for offers", "home", "rep-home"),
            new("Job history", "history", "job-history"),
            new("Help & support", "help", "help"),
            new("Release vehicle", "local_parking", ReleaseActionKey, IsDestructive: true),
            new("Log out", "power_settings_new", LogoutActionKey, IsDestructive: true),
        },
        UserRole.Dispatcher => new List<PersonaMenuItem>
        {
            new("Profile", "person", "profile"),
            new("Settings", "settings", "settings"),
            new("Log out", "power_settings_new", LogoutActionKey, IsDestructive: true),
        },
        UserRole.Requester => new List<PersonaMenuItem>
        {
            new("Log out", "power_settings_new", LogoutActionKey, IsDestructive: true),
        },
        _ => new List<PersonaMenuItem>
        {
            new("Log out", "power_settings_new", LogoutActionKey, IsDestructive: true),
        }
    };
}
