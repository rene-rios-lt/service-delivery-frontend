using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.Desktop.Services;
using ServiceDelivery.Client.UI.Features.Authentication.Services;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Desktop;

public static class MauiProgram
{
	private const string ApiBaseAddress = "http://localhost:5180";

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddMudServices();

		builder.Services.AddScoped<ITokenStore, SecureStorageTokenStore>();
		// BlazorPersonaNavigator now depends on IJobOfferStore (FE-008). Desktop does not host the
		// ServiceRep persona, but the navigator is registered in every host, so the store must be
		// resolvable here too. A lightweight scoped no-op-by-absence store satisfies the dependency.
		builder.Services.AddScoped<IJobOfferStore, InMemoryJobOfferStore>();
		// Job-offer accept service (FE-009). Desktop does not host the ServiceRep job-offer screen,
		// but the service is registered in every host (same pattern as IJobOfferStore above) so the
		// dependency is resolvable wherever the JobOffer page could render.
		builder.Services.AddScoped<IJobOfferService, HttpJobOfferService>();
		// Job-offer decline service (FE-010). Registered in every host for the same reason as the
		// accept service above — the JobOffer page injects IDeclineOfferService, so it must be
		// resolvable here too.
		builder.Services.AddScoped<IDeclineOfferService, HttpDeclineOfferService>();
		// Active job service (FE-011). Desktop does not host the ServiceRep active-job screen, but the
		// service is registered in every host (same register-everywhere pattern as the offer services
		// above) so the dependency graph resolves regardless of which persona a host renders.
		builder.Services.AddScoped<IActiveJobService, HttpActiveJobService>();
		builder.Services.AddScoped<IPersonaNavigator, BlazorPersonaNavigator>();
		builder.Services.AddScoped<ISessionExpiryHandler, SessionExpiryHandler>();
		builder.Services.AddScoped<ISessionState, SessionState>();
		builder.Services.AddScoped<SessionExpiryHttpHandler>();
		builder.Services.AddScoped<AuthTokenHttpHandler>();

		// Outbound pipeline: SessionExpiryHttpHandler (reacts to a 401 → clears session + redirects)
		// -> AuthTokenHttpHandler (attaches the JWT to every request) -> network.
		builder.Services.AddScoped(sp =>
		{
			var expiryHandler = sp.GetRequiredService<SessionExpiryHttpHandler>();
			var authHandler = sp.GetRequiredService<AuthTokenHttpHandler>();
			authHandler.InnerHandler = new HttpClientHandler();
			expiryHandler.InnerHandler = authHandler;
			return new HttpClient(expiryHandler) { BaseAddress = new Uri(ApiBaseAddress) };
		});

		builder.Services.AddScoped<IAuthService, HttpAuthService>();
		builder.Services.AddScoped<LoginViewModel>();
		builder.Services.AddScoped<AppStartViewModel>();

		// Persona shell (FE-021). Desktop presents the menu as an account dropdown. The logout
		// side-effect and release-vehicle action default to honest null-objects; FE-023 and FE-014
		// replace these registrations with their real implementations (Open/Closed — no shell change).
		builder.Services.AddScoped<IShellPresentation, DesktopShellPresentation>();
		builder.Services.AddScoped<ILogoutSideEffect, NoOpLogoutSideEffect>();
		builder.Services.AddScoped<IReleaseVehicleAction, NoOpReleaseVehicleAction>();
		builder.Services.AddScoped<PersonaMenuFactory>();
		builder.Services.AddScoped<ShellViewModel>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
