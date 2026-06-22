using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Desktop.Services;

/// <summary>
/// Desktop presentation seam: the persona menu is an account dropdown anchored to the avatar.
/// </summary>
public class DesktopShellPresentation : IShellPresentation
{
    public ShellMenuStyle MenuStyle => ShellMenuStyle.AccountMenu;
}
