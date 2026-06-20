using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.Core.ViewModels;

public class LoginViewModel
{
    private readonly IAuthService _authService;
    private readonly ITokenStore _tokenStore;
    private readonly IPersonaNavigator _navigator;

    public LoginViewModel(IAuthService authService, ITokenStore tokenStore, IPersonaNavigator navigator)
    {
        _authService = authService;
        _tokenStore = tokenStore;
        _navigator = navigator;
    }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public bool IsBusy { get; private set; }

    public const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task LoginAsync()
    {
        IsBusy = true;

        try
        {
            var response = await _authService.LoginAsync(new LoginRequest(Email, Password));

            if (response is null)
            {
                ErrorMessage = InvalidCredentialsMessage;
                return;
            }

            await _tokenStore.SetTokenAsync(response.Token);

            var profile = await _authService.GetCurrentUserAsync();

            _navigator.NavigateToPersonaHome(profile.Role);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
