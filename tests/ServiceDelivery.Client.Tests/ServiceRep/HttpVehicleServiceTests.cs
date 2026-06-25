using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Models;
using ServiceDelivery.Client.UI.Features.ServiceRep.Services;

namespace ServiceDelivery.Client.Tests.ServiceRep;

public class HttpVehicleServiceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode status, string body = "")
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static HttpVehicleService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GivenIdleVehiclesEndpointReturnsJson_WhenGetIdleVehiclesAsync_ThenVehiclesAreDeserialized()
    {
        // Arrange
        // Matches the backend AvailableVehicleDto shape: vehicleId, registration, model, equipment.
        const string json = """
            [
              { "vehicleId": "11111111-1111-1111-1111-111111111111",
                "registration": "IA-4471", "model": "Transit 350",
                "equipment": ["Hydraulics", "Coolant"] }
            ]
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        // Act
        var vehicles = await service.GetIdleVehiclesAsync();

        // Assert
        Assert.Single(vehicles);
        Assert.Equal("IA-4471", vehicles[0].Registration);
        Assert.Contains("Hydraulics", vehicles[0].EquipmentTypes);
    }

    [Fact]
    public async Task GivenIdleVehiclesEndpointReturnsJson_WhenGetIdleVehiclesAsync_ThenModelFieldIsDeserialized()
    {
        // Arrange
        // The backend AvailableVehicleDto carries the vehicle model under the camelCase "model" key.
        const string json = """
            [
              { "vehicleId": "11111111-1111-1111-1111-111111111111",
                "registration": "IA-4471", "model": "Transit 350",
                "equipment": ["Hydraulics", "Coolant"] }
            ]
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        // Act
        var vehicles = await service.GetIdleVehiclesAsync();

        // Assert
        Assert.Equal("Transit 350", vehicles[0].Model);
    }

    [Fact]
    public async Task GivenIdleVehiclesEndpoint_WhenGetIdleVehiclesAsync_ThenItCallsTheAvailableVehiclesRoute()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK, "[]");
        var service = CreateService(handler);

        // Act
        await service.GetIdleVehiclesAsync();

        // Assert
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.EndsWith("vehicles/available", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GivenTakeOverEndpointReturns200_WhenTakeOverAsync_ThenResultIsSuccess()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        var result = await service.TakeOverAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(TakeOverResult.Success, result);
    }

    [Fact]
    public async Task GivenTakeOverEndpointReturns409_WhenTakeOverAsync_ThenResultIsConflict()
    {
        // Arrange
        var handler = new StubHandler(HttpStatusCode.Conflict);
        var service = CreateService(handler);

        // Act
        var result = await service.TakeOverAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(TakeOverResult.Conflict, result);
    }

    [Fact]
    public async Task GivenAVehicleId_WhenTakeOverAsync_ThenItPostsToTheTakeOverRoute()
    {
        // Arrange
        var vehicleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var handler = new StubHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        // Act
        await service.TakeOverAsync(vehicleId);

        // Assert
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.EndsWith($"vehicles/{vehicleId}/take-over", handler.LastRequest!.RequestUri!.AbsolutePath);
    }
}
