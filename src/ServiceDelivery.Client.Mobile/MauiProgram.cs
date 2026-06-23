using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;
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

		// Every outbound request flows through SessionExpiryHttpHandler so a 401 clears the
		// session and redirects to login.
		builder.Services.AddScoped(sp =>
		{
			var expiryHandler = sp.GetRequiredService<SessionExpiryHttpHandler>();
			expiryHandler.InnerHandler = new HttpClientHandler();
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

		// Idle / waiting-for-offers view (FE-020). The RepHub client is push-driven — no polling.
		// RepIdleViewModel needs the rep's claimed vehicle; FE-007's take-over does not yet carry
		// vehicle details back (TakeOverResult is success/conflict only), so for the POC the session's
		// claimed vehicle resolves to the seeded demo truck. Wiring the real take-over hand-off of
		// claimed-vehicle data is a follow-on — no FE-020 AC depends on it.
		builder.Services.AddScoped<IRepHubService, SignalRRepHubService>();
		builder.Services.AddScoped(_ => new ClaimedVehicle(
			Guid.Empty, "IA-4471", "Transit 350",
			new[] { "Hydraulics", "Coolant", "Diagnostics" }));
		builder.Services.AddScoped<RepIdleViewModel>();

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
