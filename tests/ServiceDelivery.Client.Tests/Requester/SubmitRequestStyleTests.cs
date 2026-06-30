using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ServiceDelivery.Client.Tests.Maps;

namespace ServiceDelivery.Client.Tests.Requester;

/// <summary>
/// FE-015 styling guard (AI-review cycle-1 finding / QUAL-001 masking-test rule). The bUnit component
/// tests assert class-string PRESENCE in the rendered markup but cannot prove the class is actually
/// DEFINED/applied — Blazor scoped CSS is locked to its own component via the generated b-&lt;hash&gt;
/// attribute, so an sd-* class referenced in the markup but only defined in another page's scoped CSS
/// (or nowhere) renders unstyled. This source-read guard closes that gap: it asserts every sd-* class the
/// markup uses is defined in the page's OWN scoped stylesheet (SubmitRequest.razor.css). It mirrors the
/// existing googleMap.js / GoogleMap.razor.css source-read guards in GoogleMapComponentTests. The live
/// render check + E2E remain the ultimate visual net; this catches the "referenced-but-undefined" class.
/// </summary>
public class SubmitRequestStyleTests
{
    private static string ComponentDir => Path.Combine(
        "src", "ServiceDelivery.Client.UI", "Features", "Requester", "Pages");

    private static string Markup => File.ReadAllText(
        RepoRoot.Combine(ComponentDir.Split(Path.DirectorySeparatorChar).Append("SubmitRequest.razor").ToArray()));

    private static string Css => File.ReadAllText(
        RepoRoot.Combine(ComponentDir.Split(Path.DirectorySeparatorChar).Append("SubmitRequest.razor.css").ToArray()));

    [Fact]
    public void GivenSubmitRequestMarkup_WhenEverySdClassIsChecked_ThenEachIsDefinedInTheScopedStylesheet()
    {
        // Arrange
        var css = Css;
        var usedClasses = ExtractSdClasses(Markup);

        // Act
        var undefined = usedClasses
            .Where(cls => !Regex.IsMatch(css, $@"\.{Regex.Escape(cls)}(?![\w-])"))
            .ToList();

        // Assert
        Assert.True(
            undefined.Count == 0,
            $"SubmitRequest.razor uses sd-* class(es) not defined in SubmitRequest.razor.css "
            + $"(scoped CSS does not inherit other pages' rules): {string.Join(", ", undefined)}");
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
