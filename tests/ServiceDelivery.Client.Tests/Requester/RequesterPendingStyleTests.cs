using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ServiceDelivery.Client.Tests.Maps;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// FE-016 styling guard (AI-review cycle-1 finding) — the identical defect class fixed for FE-015 by
/// <c>SubmitRequestStyleTests</c>. The bUnit component tests assert class-string PRESENCE in the rendered
/// markup but cannot prove the class is actually DEFINED/applied — Blazor scoped CSS is locked to its own
/// component via the generated b-&lt;hash&gt; attribute, so an sd-* class referenced in the markup but only
/// defined in another page's scoped CSS (JobOffer/ActiveJob/RepIdle) or nowhere renders unstyled (the tier
/// badge as bare text, the card as an unstyled box). This source-read guard closes that gap: it asserts
/// every sd-* class the markup uses is defined in the page's OWN scoped stylesheet
/// (RequesterPending.razor.css), and explicitly covers all three tier modifiers — which the markup injects
/// at runtime via the @TierBadgeClass expression rather than as a literal class attribute, so the
/// attribute scan alone would not catch a missing modifier. The live render check + E2E remain the
/// ultimate visual net; this catches the "referenced-but-undefined" class.
/// </summary>
public class RequesterPendingStyleTests
{
    private static string ComponentDir => Path.Combine(
        "src", "ServiceDelivery.Client.UI", "Features", "Requester", "Pages");

    private static string Markup => File.ReadAllText(
        RepoRoot.Combine(ComponentDir.Split(Path.DirectorySeparatorChar).Append("RequesterPending.razor").ToArray()));

    private static string Css => File.ReadAllText(
        RepoRoot.Combine(ComponentDir.Split(Path.DirectorySeparatorChar).Append("RequesterPending.razor.css").ToArray()));

    private static bool IsDefined(string css, string cls) =>
        Regex.IsMatch(css, $@"\.{Regex.Escape(cls)}(?![\w-])");

    [Fact]
    public void GivenRequesterPendingMarkup_WhenEverySdClassIsChecked_ThenEachIsDefinedInTheScopedStylesheet()
    {
        // Arrange
        var css = Css;
        var usedClasses = ExtractSdClasses(Markup);

        // Act
        var undefined = usedClasses
            .Where(cls => !IsDefined(css, cls))
            .ToList();

        // Assert
        Assert.True(
            undefined.Count == 0,
            $"RequesterPending.razor uses sd-* class(es) not defined in RequesterPending.razor.css "
            + $"(scoped CSS does not inherit other pages' rules): {string.Join(", ", undefined)}");
    }

    [Theory]
    [InlineData("sd-badge--gold")]
    [InlineData("sd-badge--silver")]
    [InlineData("sd-badge--bronze")]
    public void GivenATierModifier_WhenChecked_ThenItIsDefinedInTheScopedStylesheet(string modifier)
    {
        // Arrange — the tier badge picks its modifier at runtime via @TierBadgeClass (BUG-034: the badge
        // reflects the requester's REAL tier, never a hardcoded GOLD), so each modifier appears in the
        // markup as a Razor expression, not a literal class="..." token. The attribute scan above would
        // miss a missing modifier; this asserts all three are defined so the pill is correctly coloured
        // for every requester tier (gold1 → gold, silver1 → silver, bronze1 → bronze).
        var css = Css;

        // Act
        var defined = IsDefined(css, modifier);

        // Assert
        Assert.True(
            defined,
            $"Tier modifier .{modifier} is not defined in RequesterPending.razor.css — the badge would "
            + "render uncoloured for that requester tier.");
    }

    // Pulls every sd-* token out of the markup's class="..." attributes AND MudBlazor's Class="..." CSS
    // parameter (capital C) — the sd-card pill is attached to a MudCard via Class=, so a lowercase-only
    // scan would miss it. MudBlazor utility classes (mt-3, font-weight-bold, …) are ignored — only sd-*
    // tokens, which are this page's own scoped styling and must be defined in its scoped stylesheet, are
    // checked.
    private static HashSet<string> ExtractSdClasses(string markup)
    {
        var result = new HashSet<string>();
        foreach (System.Text.RegularExpressions.Match attr in Regex.Matches(markup, @"(?i:class)=""([^""]*)"""))
        {
            foreach (var token in attr.Groups[1].Value.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith("sd-"))
                {
                    result.Add(token);
                }
            }
        }

        return result;
    }
}
