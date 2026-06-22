using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

/// <summary>
/// Narrow seam (ISP) supplying the platform-adaptive menu presentation. Mobile hosts return
/// <see cref="ShellMenuStyle.Drawer"/>; Desktop/Web hosts return <see cref="ShellMenuStyle.AccountMenu"/>.
/// </summary>
public interface IShellPresentation
{
    ShellMenuStyle MenuStyle { get; }
}
