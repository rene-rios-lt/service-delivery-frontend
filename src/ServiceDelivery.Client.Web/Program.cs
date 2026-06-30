using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Authentication.Services;
using ServiceDelivery.Client.UI.Features.Maps.Services;
using ServiceDelivery.Client.UI.Features.Requester.Services;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;
using ServiceDelivery.Client.Web;
using ServiceDelivery.Client.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

builder.Services.AddScoped<ITokenStore, BrowserTokenStore>();
// BlazorPersonaNavigator now depends on IJobOfferStore (FE-008). Web does not host the ServiceRep
// persona, but the navigator is registered in every host, so the store must be resolvable here too.
builder.Services.AddScoped<IJobOfferStore, InMemoryJobOfferStore>();
// Claimed-vehicle hand-off store (BUG-034). Web does not host the ServiceRep idle view, but the store
// is registered in every host (same register-everywhere pattern as IJobOfferStore above) so the
// dependency graph resolves wherever RepIdleViewModel could be constructed.
builder.Services.AddScoped<IClaimedVehicleStore, InMemoryClaimedVehicleStore>();
// Job-offer accept service (FE-009). Web does not host the ServiceRep job-offer screen, but the
// service is registered in every host (same pattern as IJobOfferStore above) so the dependency is
// resolvable wherever the JobOffer page could render.
builder.Services.AddScoped<IJobOfferService, HttpJobOfferService>();
// Job-offer decline service (FE-010). Registered in every host for the same reason as the accept
// service above — the JobOffer page injects IDeclineOfferService, so it must be resolvable here too.
builder.Services.AddScoped<IDeclineOfferService, HttpDeclineOfferService>();
// Active job service (FE-011). Web does not host the ServiceRep active-job screen, but the service
// is registered in every host (same register-everywhere pattern as the offer services above) so the
// dependency graph resolves regardless of which persona a host renders.
builder.Services.AddScoped<IActiveJobService, HttpActiveJobService>();
// Mark-complete action (FE-013). Web does not host the ServiceRep active-job screen, but the service
// is registered in every host (same register-everywhere pattern as the active-job service above) so
// the dependency graph resolves regardless of which persona a host renders.
builder.Services.AddScoped<ICompleteJobService, HttpCompleteJobService>();
// Google Maps SDK loading + key config (FE-025). The key is read from GoogleMaps:ApiKey in
// IConfiguration — WASM auto-loads wwwroot/appsettings.json (committed placeholder, empty key); a
// gitignored wwwroot/appsettings.Local.json supplies the real key for local dev (see docs/maps-api-key.md).
// MapsLoader injects the SDK <script> only when a non-blank key is present (FE-024 consumes the result).
builder.Services.AddScoped<IMapsKeyProvider, ConfigurationMapsKeyProvider>();
// FE-024: the GoogleMap component injects IMapsLoader (Dependency Inversion). Register the abstraction
// against the concrete, and keep the concrete resolvable for any caller that still depends on it directly.
builder.Services.AddScoped<MapsLoader>();
builder.Services.AddScoped<IMapsLoader>(sp => sp.GetRequiredService<MapsLoader>());
builder.Services.AddScoped<IPersonaNavigator, BlazorPersonaNavigator>();
// Requester submit form (FE-015). Web hosts the Requester persona, so these are live here. The DTC and
// service-request services are Blazor-generic (HttpClient); the geolocation service is the browser/WASM
// implementation. SubmitRequestViewModel orchestrates the form. Registered in every host for parity.
builder.Services.AddScoped<IDtcService, HttpDtcService>();
builder.Services.AddScoped<IServiceRequestService, HttpServiceRequestService>();
builder.Services.AddScoped<IGeolocationService, BrowserGeolocationService>();
builder.Services.AddScoped<SubmitRequestViewModel>();
// Requester pending / "finding your technician" view (FE-016). Web hosts the Requester persona. The
// RequesterHub client is push-driven (no polling); RequesterPendingViewModel sources the requester's
// real tier from IAuthService and navigates to the tracking view on RepAssigned. Registered in every
// host for parity (the Requester persona is supported on Web, Desktop, and Mobile).
builder.Services.AddScoped<IRequesterHubService, SignalRRequesterHubService>();
builder.Services.AddScoped<RequesterPendingViewModel>();
builder.Services.AddScoped<ISessionExpiryHandler, SessionExpiryHandler>();
builder.Services.AddScoped<ISessionState, SessionState>();
builder.Services.AddScoped<SessionExpiryHttpHandler>();
builder.Services.AddScoped<AuthTokenHttpHandler>();

// Backend API base address (local HTTP profile from scripts/local/start.sh). Outbound pipeline:
// SessionExpiryHttpHandler (reacts to 401) -> AuthTokenHttpHandler (attaches the JWT) -> network.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "http://localhost:5180";
builder.Services.AddScoped(sp =>
{
    var expiryHandler = sp.GetRequiredService<SessionExpiryHttpHandler>();
    var authHandler = sp.GetRequiredService<AuthTokenHttpHandler>();
    authHandler.InnerHandler = new HttpClientHandler();
    expiryHandler.InnerHandler = authHandler;
    return new HttpClient(expiryHandler) { BaseAddress = new Uri(apiBaseAddress) };
});

builder.Services.AddScoped<IAuthService, HttpAuthService>();
builder.Services.AddScoped<LoginViewModel>();
builder.Services.AddScoped<AppStartViewModel>();

// Persona shell (FE-021). Web presents the menu as an account dropdown. The logout side-effect and
// release-vehicle action default to honest null-objects; FE-023 and FE-014 replace these
// registrations with their real implementations (Open/Closed — no shell change).
builder.Services.AddScoped<IShellPresentation, WebShellPresentation>();
builder.Services.AddScoped<ILogoutSideEffect, NoOpLogoutSideEffect>();
builder.Services.AddScoped<IReleaseVehicleAction, NoOpReleaseVehicleAction>();
builder.Services.AddScoped<PersonaMenuFactory>();
builder.Services.AddScoped<ShellViewModel>();

await builder.Build().RunAsync();
