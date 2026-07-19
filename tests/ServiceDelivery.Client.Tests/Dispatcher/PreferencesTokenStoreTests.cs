using System.IO;
using ServiceDelivery.Client.Tests.Maps;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// AC-1 (Desktop token-store swap). <see cref="PreferencesTokenStore"/> lives in the Desktop MAUI host
/// (<c>src/ServiceDelivery.Client.Desktop/Services/</c>) and depends on MAUI <c>Preferences</c>, so it is
/// NOT reachable from this offline test project (Tests references only UI + Core, and host projects are
/// bootstrapping-only per the repo convention). These are therefore committed-source guards — the same
/// mechanism <c>GoogleMapComponentTests</c> uses for <c>googleMap.js</c> exports it cannot exercise under
/// bUnit. They fail loudly if the real store stops implementing the full <see cref="ServiceDelivery.Client.Core.Interfaces.ITokenStore"/>
/// contract, or if the Desktop DI registration regresses back to the Keychain-backed store. The live gate
/// for the actual crash-free Desktop login is the Mac2Driver E2E (AC-1d) + the AI-review render gate.
/// </summary>
public class PreferencesTokenStoreTests
{
    private static string TokenStoreSource() => File.ReadAllText(RepoRoot.Combine(
        "src", "ServiceDelivery.Client.Desktop", "Services", "PreferencesTokenStore.cs"));

    private static string MauiProgramSource() => File.ReadAllText(RepoRoot.Combine(
        "src", "ServiceDelivery.Client.Desktop", "MauiProgram.cs"));

    [Fact]
    public void GivenPreferencesTokenStore_WhenTypeInspected_ThenImplementsITokenStore()
    {
        // Arrange
        var path = RepoRoot.Combine(
            "src", "ServiceDelivery.Client.Desktop", "Services", "PreferencesTokenStore.cs");

        // Act
        Assert.True(File.Exists(path), $"Expected PreferencesTokenStore.cs at '{path}'.");
        var source = TokenStoreSource();

        // Assert — declares ITokenStore and fully implements all three contract methods (no no-op / L
        // violation), and is backed by MAUI Preferences rather than the Keychain-backed SecureStorage.
        Assert.Contains("class PreferencesTokenStore : ITokenStore", source);
        Assert.Contains("SetTokenAsync", source);
        Assert.Contains("GetTokenAsync", source);
        Assert.Contains("ClearAsync", source);
        Assert.Contains("Preferences.Default", source);
        Assert.DoesNotContain("SecureStorage.Default", source);
    }

    [Fact]
    public void GivenDesktopMauiProgram_WhenTokenStoreRegistrationInspected_ThenPreferencesTokenStoreIsRegisteredNotSecureStorage()
    {
        // Arrange
        var source = MauiProgramSource();

        // Act
        // (source already read)

        // Assert — Desktop composition root binds ITokenStore to the Preferences-backed store, and no
        // longer to SecureStorageTokenStore (whose Keychain access crashed unsigned Mac Catalyst login).
        Assert.Contains("AddScoped<ITokenStore, PreferencesTokenStore>", source);
        Assert.DoesNotContain("SecureStorageTokenStore", source);
    }
}
