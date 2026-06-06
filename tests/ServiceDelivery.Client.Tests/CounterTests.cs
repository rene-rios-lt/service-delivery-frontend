using Bunit;
using ServiceDelivery.Client.UI.Features.Dashboard.Pages;

namespace ServiceDelivery.Client.Tests;

public class CounterTests : BunitContext
{
    [Fact]
    public void Counter_InitialCount_IsZero()
    {
        var cut = Render<Counter>();

        cut.Find("p[role=status]").MarkupMatches("<p role=\"status\">Current count: 0</p>");
    }

    [Fact]
    public void Counter_AfterOneClick_CountIsOne()
    {
        var cut = Render<Counter>();

        cut.Find("button").Click();

        cut.Find("p[role=status]").MarkupMatches("<p role=\"status\">Current count: 1</p>");
    }

    [Fact]
    public void Counter_AfterMultipleClicks_CountIncreasesCorrectly()
    {
        var cut = Render<Counter>();

        cut.Find("button").Click();
        cut.Find("button").Click();
        cut.Find("button").Click();

        cut.Find("p[role=status]").MarkupMatches("<p role=\"status\">Current count: 3</p>");
    }
}
