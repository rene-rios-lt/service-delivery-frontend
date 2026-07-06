using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ServiceDelivery.Client.Tests.Maps;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// FE-015 styling guard (AI-review cycle-1 finding / QUAL-001 masking-test rule), retargeted for QUAL-011.
/// The bUnit component tests assert class-string PRESENCE in the rendered markup but cannot prove the class
/// is actually DEFINED/applied. QUAL-011 lifted the shared sd-* tokens out of this page's scoped CSS into
/// the global design-system.css served by every host, leaving only page-specific rules scoped. So this
/// guard now asserts every sd-* class the markup uses is defined in the global sheet OR the page's OWN
/// scoped stylesheet (the union) — a shared token dropped without the global sheet supplying it, or a
/// page-specific token lost from the scoped file, would still be caught. The live render check + E2E
/// remain the ultimate visual net; this catches the "referenced-but-undefined" class.
/// </summary>
public class SubmitRequestStyleTests
{
    private static string ComponentDir => Path.Combine(
        "src", "ServiceDelivery.Client.UI", "Features", "Requester", "Pages");

    private static string Markup => File.ReadAllText(
        RepoRoot.Combine(ComponentDir.Split(Path.DirectorySeparatorChar).Append("SubmitRequest.razor").ToArray()));

    private static string ScopedCss => File.ReadAllText(
        RepoRoot.Combine(ComponentDir.Split(Path.DirectorySeparatorChar).Append("SubmitRequest.razor.css").ToArray()));

    private static string GlobalCss => File.ReadAllText(
        RepoRoot.Combine("src", "ServiceDelivery.Client.UI", "wwwroot", "design-system.css"));

    private static bool ResolvesFrom(string scoped, string cls) =>
        Regex.IsMatch(GlobalCss, $@"\.{Regex.Escape(cls)}(?![\w-])")
        || Regex.IsMatch(scoped, $@"\.{Regex.Escape(cls)}(?![\w-])");

    [Fact]
    public void GivenSubmitRequestMarkup_WhenEverySdClassIsChecked_ThenEachIsDefinedInGlobalOrScopedStylesheet()
    {
        // Arrange
        var scoped = ScopedCss;
        var usedClasses = ExtractSdClasses(Markup);

        // Act
        var undefined = usedClasses
            .Where(cls => !ResolvesFrom(scoped, cls))
            .ToList();

        // Assert
        Assert.True(
            undefined.Count == 0,
            "SubmitRequest.razor uses sd-* class(es) defined neither in the global design-system.css nor "
            + $"in SubmitRequest.razor.css: {string.Join(", ", undefined)}");
    }

    // Pulls every sd-* token out of the markup's class="..." attributes. MudBlazor utility classes
    // (mt-3, font-weight-bold, …) and MudComponent CSS-class params are ignored — only sd-* tokens, which
    // are this page's own scoped styling and must be defined in its scoped stylesheet, are checked.
    private static HashSet<string> ExtractSdClasses(string markup)
    {
        var result = new HashSet<string>();
        foreach (System.Text.RegularExpressions.Match attr in Regex.Matches(markup, @"class=""([^""]*)"""))
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
