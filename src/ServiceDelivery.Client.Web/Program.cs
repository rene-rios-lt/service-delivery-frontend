using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Authentication.Services;
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
builder.Services.AddScoped<IPersonaNavigator, BlazorPersonaNavigator>();
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
