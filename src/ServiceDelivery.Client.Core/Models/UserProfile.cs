namespace ServiceDelivery.Client.Core.Models;

public record UserProfile(
    Guid UserId,
    string Name,
    UserRole Role,
    ServiceTier Tier,
    Guid DealerId);
