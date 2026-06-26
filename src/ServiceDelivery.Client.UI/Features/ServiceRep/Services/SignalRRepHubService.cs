using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IRepHubService"/> backed by a real SignalR <see cref="HubConnection"/>.
/// Shared by every host because the hub contract is platform-agnostic. The hub URL is resolved from
/// the same <see cref="HttpClient"/> base address the rest of the app uses (so it always targets the
/// configured backend) plus the RepHub path <c>/hubs/rep</c>. This adapter only manages the
/// connection lifecycle and forwards the <c>JobOfferReceived</c> event (idle screen, FE-020) and the
/// <c>RedirectReceived</c> event (active-job view, FE-011) to the registered handlers — all screen
/// logic lives in the ViewModels.
/// </summary>
public sealed class SignalRRepHubService : IRepHubService, IAsyncDisposable
{
    private const string RepHubPath = "hubs/rep";
    private const string JobOfferReceivedEvent = "JobOfferReceived";
    private const string RedirectReceivedEvent = "RedirectReceived";

    private readonly HubConnection _connection;
    private readonly ITokenStore _tokenStore;

    public SignalRRepHubService(HttpClient httpClient, ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        var hubUrl = new Uri(httpClient.BaseAddress!, RepHubPath);
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options => options.AccessTokenProvider = ProvideAccessTokenAsync)
            // The backend serializes JobOfferReceivedPayload.requesterTier as a string ("Gold"), so the
            // RepHub connection must decode enums from their string form. This converter is scoped to
            // this connection only — REST deserialization (integer-based ServiceTier on GET /users/me)
            // is unaffected. Without it the tier deserialized to ServiceTier.None and the offer screen's
            // tier badge rendered invisible (BUG-036 AC-1). Mirrors the JsonPropertyName on JobOfferPayload.
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .WithAutomaticReconnect()
            .Build();
    }

    /// <summary>
    /// Supplies the JWT that SignalR appends as <c>?access_token=...</c> when negotiating the RepHub
    /// connection. The RepHub is <c>[Authorize]</c> and websockets cannot send an Authorization
    /// header, so this is the SignalR equivalent of <c>AuthTokenHttpHandler</c> for REST calls —
    /// without it the connection is unauthenticated and never joins its <c>rep:{userId}</c> group.
    /// </summary>
    public Task<string?> ProvideAccessTokenAsync() => _tokenStore.GetTokenAsync();

    public void OnJobOfferReceived(Func<JobOfferPayload, Task> handler) =>
        _connection.On(JobOfferReceivedEvent, handler);

    public void OnRedirectReceived(Func<RedirectPayload, Task> handler) =>
        _connection.On(RedirectReceivedEvent, handler);

    public Task StartAsync() => _connection.StartAsync();

    public Task StopAsync() => _connection.StopAsync();

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
