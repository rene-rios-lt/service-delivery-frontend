using ServiceDelivery.Client.Core.Formatting;

namespace ServiceDelivery.Client.Tests.Dispatcher;

/// <summary>
/// Pure unit tests for <see cref="RelativeTime.Describe"/> — the "1 min ago" / "22 min ago" relative-time
/// text the request card shows (FE-004 AC-2). Deterministic because "now" is passed in, so it never depends
/// on the wall clock.
/// </summary>
public class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenACreatedAtUnderAMinuteAgo_WhenDescribed_ThenReadsJustNow()
    {
        // Arrange
        var createdAt = Now.AddSeconds(-20);

        // Act
        var text = RelativeTime.Describe(createdAt, Now);

        // Assert
        Assert.Equal("just now", text);
    }

    [Theory]
    [InlineData(1, "1 min ago")]
    [InlineData(4, "4 min ago")]
    [InlineData(22, "22 min ago")]
    public void GivenACreatedAtSomeMinutesAgo_WhenDescribed_ThenReadsNMinAgo(int minutes, string expected)
    {
        // Arrange
        var createdAt = Now.AddMinutes(-minutes).AddSeconds(-5);

        // Act
        var text = RelativeTime.Describe(createdAt, Now);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void GivenACreatedAtOverAnHourAgo_WhenDescribed_ThenReadsNHrAgo()
    {
        // Arrange
        var createdAt = Now.AddMinutes(-90);

        // Act
        var text = RelativeTime.Describe(createdAt, Now);

        // Assert
        Assert.Equal("1 hr ago", text);
    }
}
