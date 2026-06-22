using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using ServiceDelivery.Client.Core.Authentication;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Services;
using ServiceDelivery.Client.Core.ViewModels;
using ServiceDelivery.Client.UI.Features.Authentication.Services;
using ServiceDelivery.Client.Web;
using ServiceDelivery.Client.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

builder.Services.AddScoped<ITokenStore, BrowserTokenStore>();
builder.Services.AddScoped<IPersonaNavigator, BlazorPersonaNavigator>();
builder.Services.AddScoped<ISessionExpiryHandler, SessionExpiryHandler>();
builder.Services.AddScoped<ISessionState, SessionState>();
builder.Services.AddScoped<SessionExpiryHttpHandler>();

// Backend API base address (local HTTP profile from scripts/local/start.sh). Every outbound
// request flows through SessionExpiryHttpHandler so a 401 clears the session and redirects.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "http://localhost:5180";
builder.Services.AddScoped(sp =>
{
    var expiryHandler = sp.GetRequiredService<SessionExpiryHttpHandler>();
    expiryHandler.InnerHandler = new HttpClientHandler();
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
