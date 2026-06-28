using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.Desktop.Services;
using ServiceDelivery.Client.UI.Features.Authentication.Services;
using ServiceDelivery.Client.UI.Features.Maps.Services;
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

		// Maps key configuration (FE-025). MAUI hosts do not load appsettings.json by default, so we add it
		// here: the committed Resources/Raw/appsettings.json carries an empty GoogleMaps:ApiKey placeholder;
		// a gitignored Resources/Raw/appsettings.Local.json (when present) layers the real key on top.
		// ConfigurationMapsKeyProvider then reads GoogleMaps:ApiKey from IConfiguration. (This only adds a
		// maps-key config source; ApiBaseAddress stays the hardcoded const below.)
		AddMapsConfiguration(builder.Configuration);

		// Google Maps SDK loading + key provider (FE-025). Desktop is a Dispatcher/Requester host that will
		// render the fleet/tracking map; the provider reads GoogleMaps:ApiKey and MapsLoader injects the SDK
		// <script> only when a non-blank key is present (FE-024 consumes the MapsAvailability result).
		builder.Services.AddScoped<IMapsKeyProvider, ConfigurationMapsKeyProvider>();
		// FE-024: the GoogleMap component injects IMapsLoader (Dependency Inversion). Register the
		// abstraction against the concrete, and keep the concrete resolvable for direct callers.
		builder.Services.AddScoped<MapsLoader>();
		builder.Services.AddScoped<IMapsLoader>(sp => sp.GetRequiredService<MapsLoader>());

		builder.Services.AddScoped<ITokenStore, SecureStorageTokenStore>();
		// BlazorPersonaNavigator now depends on IJobOfferStore (FE-008). Desktop does not host the
		// ServiceRep persona, but the navigator is registered in every host, so the store must be
		// resolvable here too. A lightweight scoped no-op-by-absence store satisfies the dependency.
		builder.Services.AddScoped<IJobOfferStore, InMemoryJobOfferStore>();
		// Claimed-vehicle hand-off store (BUG-034). Desktop does not host the ServiceRep idle view,
		// but the store is registered in every host (same register-everywhere pattern as IJobOfferStore
		// above) so the dependency graph resolves wherever RepIdleViewModel could be constructed.
		builder.Services.AddScoped<IClaimedVehicleStore, InMemoryClaimedVehicleStore>();
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
		// Mark-complete action (FE-013). Desktop does not host the ServiceRep active-job screen, but the
		// service is registered in every host (same register-everywhere pattern as the active-job service
		// above) so the dependency graph resolves regardless of which persona a host renders.
		builder.Services.AddScoped<ICompleteJobService, HttpCompleteJobService>();
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

	// Loads the Google Maps key config for this MAUI host (FE-025). appsettings.json is shipped as a
	// MauiAsset (Resources/Raw); it is read from the app package and added as a JSON config stream so
	// GoogleMaps:ApiKey becomes available via IConfiguration. A gitignored appsettings.Local.json (also a
	// MauiAsset when a developer drops one in) layers the real key on top. Reads run synchronously at
	// startup — there is no usable IConfiguration value before the host is built.
	private static void AddMapsConfiguration(IConfigurationManager configuration)
	{
		AddJsonAssetIfPresent(configuration, "appsettings.json");
		AddJsonAssetIfPresent(configuration, "appsettings.Local.json");
	}

	private static void AddJsonAssetIfPresent(IConfigurationManager configuration, string fileName)
	{
		try
		{
			using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
			configuration.AddJsonStream(stream);
		}
		catch (FileNotFoundException)
		{
			// Optional source (e.g. the gitignored appsettings.Local.json on a clean checkout). The
			// committed placeholder keeps GoogleMaps:ApiKey present-but-empty, and MapsLoader's blank-key
			// guard (FE-025 AC-3) handles the absent-key case without crashing.
		}
	}
}
