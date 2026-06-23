using Microsoft.AspNetCore.SignalR.Client;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.ServiceRep.Services;

/// <summary>
/// Blazor-generic <see cref="IRepHubService"/> backed by a real SignalR <see cref="HubConnection"/>.
/// Shared by every host because the hub contract is platform-agnostic. The hub URL is resolved from
/// the same <see cref="HttpClient"/> base address the rest of the app uses (so it always targets the
/// configured backend) plus the RepHub path <c>/hubs/rep</c>. This adapter only manages the
/// connection lifecycle and forwards the single <c>JobOfferReceived</c> event to the registered
/// handler — all idle-screen logic lives in <see cref="ServiceDelivery.Client.Core.ViewModels.RepIdleViewModel"/>.
/// </summary>
public sealed class SignalRRepHubService : IRepHubService, IAsyncDisposable
{
    private const string RepHubPath = "hubs/rep";
    private const string JobOfferReceivedEvent = "JobOfferReceived";

    private readonly HubConnection _connection;

    public SignalRRepHubService(HttpClient httpClient)
    {
        var hubUrl = new Uri(httpClient.BaseAddress!, RepHubPath);
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();
    }

    public void OnJobOfferReceived(Func<JobOfferPayload, Task> handler) =>
        _connection.On(JobOfferReceivedEvent, handler);

    public Task StartAsync() => _connection.StartAsync();

    public Task StopAsync() => _connection.StopAsync();

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
