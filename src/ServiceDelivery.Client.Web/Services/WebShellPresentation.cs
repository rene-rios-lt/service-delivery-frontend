using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Web.Services;

/// <summary>
/// Web presentation seam: the persona menu is an account dropdown anchored to the avatar.
/// </summary>
public class WebShellPresentation : IShellPresentation
{
    public ShellMenuStyle MenuStyle => ShellMenuStyle.AccountMenu;
}
