using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.Interfaces;

public interface IPersonaNavigator
{
    void NavigateToPersonaHome(UserRole role);
}
