using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.Core.Models;

namespace ServiceDelivery.Client.UI.Features.Maps.Components;

/// <summary>
/// Reusable Google Maps component (FE-024). Owns the JS-interop lifecycle for one map instance: on first
/// render it asks <see cref="IMapsLoader"/> whether the SDK is available, imports the <c>googleMap.js</c>
/// module, and initialises the map; on dispose it tears the map down. Exposes an imperative API
/// (markers / polylines / panTo / setZoom / fitBounds) that delegates straight to the module. The C# side
/// holds no map state — Single Responsibility lives on each side of the interop boundary (the module owns
/// the <c>google.maps.Map</c>; this component owns the Blazor lifecycle and the call surface).
/// </summary>
public partial class GoogleMap : IAsyncDisposable
{
    private const string ModulePath =
        "./_content/ServiceDelivery.Client.UI/Features/Maps/googleMap.js";

    [Inject] private IMapsLoader MapsLoader { get; set; } = default!;

    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    [Parameter] public double Lat { get; set; }

    [Parameter] public double Lng { get; set; }

    [Parameter] public int Zoom { get; set; }

    // Fired once the map has been initialised (the JS module is imported and initMap has run). Consumers
    // (FE-026) place their overlays in this callback rather than in their own OnAfterRenderAsync, because a
    // parent's OnAfterRenderAsync runs before this child's async module import completes — the callback is
    // the deterministic ready signal. Not fired when the SDK is unavailable (no map to draw on).
    [Parameter] public EventCallback OnMapReady { get; set; }

    private readonly string _containerId = $"sd-map-{Guid.NewGuid():N}";

    private IJSObjectReference? _module;

    private bool _mapsUnavailable;

    protected string ContainerId => _containerId;

    protected bool ShowPlaceholder => _mapsUnavailable;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        var availability = await MapsLoader.LoadAsync();
        if (!availability.IsAvailable)
        {
            _mapsUnavailable = true;
            StateHasChanged();
            return;
        }

        _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await _module.InvokeVoidAsync("initMap", _containerId, Lat, Lng, Zoom);
        await OnMapReady.InvokeAsync();
    }

    public Task AddOrUpdateMarkerAsync(string id, double lat, double lng, string colour, string testId) =>
        InvokeMapAsync("addOrUpdateMarker", id, lat, lng, colour, testId);

    public Task RemoveMarkerAsync(string id) =>
        InvokeMapAsync("removeMarker", id);

    public Task AddOrUpdatePolylineAsync(string id, IEnumerable<GpsPoint> points, string testId) =>
        InvokeMapAsync("addOrUpdatePolyline", id, points, testId);

    public Task RemovePolylineAsync(string id) =>
        InvokeMapAsync("removePolyline", id);

    public Task PanToAsync(double lat, double lng) =>
        InvokeMapAsync("panTo", lat, lng);

    public Task SetZoomAsync(int zoom) =>
        InvokeMapAsync("setZoom", zoom);

    public Task FitBoundsAsync(IEnumerable<GpsPoint> points) =>
        InvokeMapAsync("fitBounds", points);

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        await _module.InvokeVoidAsync("disposeMap", _containerId);
        await _module.DisposeAsync();
    }

    // Every map mutation targets this component's container and is a no-op until the JS module has been
    // imported (after first render). Centralising the null guard and the containerId-prepend keeps each
    // public method a single intent-revealing line and removes the per-method boilerplate.
    private async Task InvokeMapAsync(string function, params object[] args)
    {
        if (_module is null)
        {
            return;
        }

        var fullArgs = new object[args.Length + 1];
        fullArgs[0] = _containerId;
        Array.Copy(args, 0, fullArgs, 1, args.Length);
        await _module.InvokeVoidAsync(function, fullArgs);
    }
}
