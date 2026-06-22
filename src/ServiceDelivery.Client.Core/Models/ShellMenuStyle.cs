namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// The platform-adaptive presentation choice for the persona menu. The host supplies the
/// value through <c>IShellPresentation</c> so the UI never branches on platform with <c>#if</c>.
/// </summary>
public enum ShellMenuStyle
{
    Drawer,
    AccountMenu
}
