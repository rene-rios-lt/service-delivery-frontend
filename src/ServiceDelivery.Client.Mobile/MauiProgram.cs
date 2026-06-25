using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.Mobile.Services;
using ServiceDelivery.Client.UI.Features.Authentication.Services;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Mobile;

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
		// Job-offer handoff store (FE-008). The navigator deposits the in-flight JobOfferReceived
		// payload here before navigating to /rep/offer; the page reads it on init. Scoped so the
		// navigator and the page share one instance within a session.
		builder.Services.AddScoped<IJobOfferStore, InMemoryJobOfferStore>();
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

		// Rep take-over screen (FE-007). The HTTP vehicle service backs the idle-vehicle list and
		// the take-over claim; the ViewModel orchestrates selection, conflict handling, and the
		// post-takeover navigation to the idle rep view.
		builder.Services.AddScoped<IVehicleService, HttpVehicleService>();
		builder.Services.AddScoped<TakeOverViewModel>();

		// Job-offer accept (FE-009). The HTTP job-offer service backs POST /job-offers/{id}/accept;
		// the JobOffer page injects it and hands it to JobOfferViewModel, which navigates to the
		// active-job view on success or back to idle (with an "Offer expired" message) on a 409.
		builder.Services.AddScoped<IJobOfferService, HttpJobOfferService>();

		// Job-offer decline (FE-010). The HTTP decline service backs POST /job-offers/{id}/decline;
		// the JobOffer page injects it and hands it to JobOfferViewModel, which returns the rep to the
		// idle / waiting-for-offers view on both success and a 409 (same outcome — the offer is gone).
		builder.Services.AddScoped<IDeclineOfferService, HttpDeclineOfferService>();

		// Idle / waiting-for-offers view (FE-020). The RepHub client is push-driven — no polling.
		// RepIdleViewModel reads the rep's claimed vehicle from IClaimedVehicleStore (BUG-034):
		// TakeOverViewModel deposits the vehicle the rep actually took over before navigating here,
		// so the card and app-bar subtitle reflect the real selection rather than a hardcoded truck.
		builder.Services.AddScoped<IRepHubService, SignalRRepHubService>();
		builder.Services.AddScoped<IClaimedVehicleStore, InMemoryClaimedVehicleStore>();
		builder.Services.AddScoped<RepIdleViewModel>();

		// Active job navigation view (FE-011). The HTTP active-job service backs
		// GET /service-requests/my-active; the ActiveJob page injects ActiveJobViewModel, which polls
		// the rep's simulator-driven position, gates the "I've Arrived" button on the within-15-miles
		// state, and listens on RepHub for RedirectReceived to move the destination in-place.
		builder.Services.AddScoped<IActiveJobService, HttpActiveJobService>();
		builder.Services.AddScoped<ActiveJobViewModel>();

		// Persona shell (FE-021). Mobile presents the menu as a slide-in drawer. The logout
		// side-effect and release-vehicle action default to honest null-objects; FE-023 and FE-014
		// replace these registrations with their real implementations (Open/Closed — no shell change).
		builder.Services.AddScoped<IShellPresentation, MobileShellPresentation>();
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
