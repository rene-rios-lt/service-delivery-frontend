namespace ServiceDelivery.Client.Core.Interfaces;

public interface ITokenStore
{
    Task SetTokenAsync(string token);

    Task<string?> GetTokenAsync();

    Task ClearAsync();
}
