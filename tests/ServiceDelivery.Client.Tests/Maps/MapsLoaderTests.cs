using System;
using System.IO;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceDelivery.Client.Core.Interfaces;
using ServiceDelivery.Client.UI.Features.Maps.Services;

namespace ServiceDelivery.Client.Tests.Maps;

public class MapsLoaderTests
{
    private const string ModulePath = "./_content/ServiceDelivery.Client.UI/Features/Maps/mapsLoader.js";

    private readonly Mock<IMapsKeyProvider> _keyProvider = new();
    private readonly BunitJSInterop _js = new();
    private readonly BunitJSModuleInterop _module;

    public MapsLoaderTests()
    {
        _module = _js.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
    }

    private MapsLoader CreateLoader(ILogger<MapsLoader>? logger = null) =>
        new(_keyProvider.Object, _js.JSRuntime, logger ?? NullLogger<MapsLoader>.Instance);

    [Fact]
    public async Task GivenAValidApiKey_WhenLoadAsyncCalled_ThenSdkScriptTagAddedWithMapsAndMarkerLibraries()
    {
        // Arrange
        _keyProvider.Setup(p => p.GetMapsApiKey()).Returns("AIza-valid-key");
        var loader = CreateLoader();

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.True(result.IsAvailable);
        var invocation = _module.VerifyInvoke("loadSdk");
        Assert.Equal("AIza-valid-key", invocation.Arguments[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GivenBlankApiKey_WhenLoadAsyncCalled_ThenIsAvailableIsFalse(string? blankKey)
    {
        // Arrange
        _keyProvider.Setup(p => p.GetMapsApiKey()).Returns(blankKey);
        var loader = CreateLoader();

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task GivenBlankApiKey_WhenLoadAsyncCalled_ThenDiagnosticMessageIsNonNull()
    {
        // Arrange
        _keyProvider.Setup(p => p.GetMapsApiKey()).Returns("");
        var loader = CreateLoader();

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.DiagnosticMessage));
    }

    [Fact]
    public async Task GivenBlankApiKey_WhenLoadAsyncCalled_ThenWarningIsLogged()
    {
        // Arrange
        _keyProvider.Setup(p => p.GetMapsApiKey()).Returns("");
        var logger = new Mock<ILogger<MapsLoader>>();
        var loader = CreateLoader(logger.Object);

        // Act
        await loader.LoadAsync();

        // Assert
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenBlankApiKey_WhenLoadAsyncCalled_ThenNoScriptTagInjected()
    {
        // Arrange — a blank key must short-circuit before any JS interop, so the SDK <script> is never
        // injected and Google's API is never hit without a key (structurally enforces AC-3's no-crash).
        _keyProvider.Setup(p => p.GetMapsApiKey()).Returns("");
        var loader = CreateLoader();

        // Act
        await loader.LoadAsync();

        // Assert
        _module.VerifyNotInvoke("loadSdk");
        Assert.Empty(_js.Invocations);
    }

    [Fact]
    public void GivenAKey_WhenLoadSdkInvoked_ThenScriptSrcContainsLibrariesMapMarker()
    {
        // Arrange
        var modulePath = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.UI", "wwwroot", "Features", "Maps", "mapsLoader.js");

        // Act
        var module = File.ReadAllText(modulePath);

        // Assert
        Assert.Contains("maps.googleapis.com/maps/api/js", module);
        Assert.Contains("libraries=maps,marker", module);
        Assert.Contains("loading=async", module);
        Assert.Contains("key=", module);
    }
}
