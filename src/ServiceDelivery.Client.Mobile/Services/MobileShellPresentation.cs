using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Mobile.Services;

/// <summary>
/// Mobile presentation seam: the persona menu is a slide-in drawer (ServiceRep on iOS/Android).
/// </summary>
public class MobileShellPresentation : IShellPresentation
{
    public ShellMenuStyle MenuStyle => ShellMenuStyle.Drawer;
}
