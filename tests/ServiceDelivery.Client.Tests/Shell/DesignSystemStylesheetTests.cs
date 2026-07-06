using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ServiceDelivery.Client.Tests.Maps;

namespace ServiceDelivery.Client.Tests.Shell;

/// <summary>
/// QUAL-011 consolidation guard. This story lifts the genuinely-shared sd-* tokens out of the five
/// page-scoped stylesheets and into one global sheet served by every host at
/// <c>_content/ServiceDelivery.Client.UI/design-system.css</c>. Blazor scoped CSS is locked to its own
/// component via the generated b-&lt;hash&gt; attribute, so before this story every page had to redefine
/// the shared tokens; the duplication drifted (e.g. the sd-btn--primary shadow diverged .06/.08 vs
/// .10/.12). These source-read guards assert:
///   AC-1 — the global sheet defines every shared token with its canonical value;
///   AC-3 — the shared token base properties are stripped from each page's scoped sheet;
///   AC-4 — every sd-* class each page's markup uses still resolves from the global sheet OR that page's
///          own retained scoped rules (no shared token dropped without the global sheet supplying it).
/// The live render check + Playwright/Appium (AC-5) remain the ultimate visual net; these catch a
/// dropped-or-undefined token at the source level.
/// </summary>
public class DesignSystemStylesheetTests
{
    private static string UiDir => Path.Combine("src", "ServiceDelivery.Client.UI");
    private static string ServiceRepPagesDir => Path.Combine(UiDir, "Features", "ServiceRep", "Pages");
    private static string RequesterPagesDir => Path.Combine(UiDir, "Features", "Requester", "Pages");

    private static string GlobalCss => ReadOrEmpty(
        RepoRoot.Combine(UiDir, "wwwroot", "design-system.css"));

    private static string ReadOrEmpty(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    private static string Read(params string[] segments) => File.ReadAllText(RepoRoot.Combine(segments));

    // A class is "defined" in a stylesheet when its selector appears — the negative lookahead stops
    // ".sd-btn" from matching ".sd-btn--block" / ".sd-btn__icon" so base and modifier are distinguished.
    private static bool Defined(string css, string cls) =>
        Regex.IsMatch(css, $@"\.{Regex.Escape(cls)}(?![\w-])");

    // AC-4: a class the markup uses "resolves" when it is defined in the global sheet OR in that page's
    // own retained scoped CSS. The union must reproduce every rule the page relied on before the move.
    private static bool ResolvesFrom(string scopedCss, string cls) =>
        Defined(GlobalCss, cls) || Defined(scopedCss, cls);

    // Pulls every sd-* token out of the markup's class="..." attributes AND MudBlazor's Class="..." CSS
    // parameter (capital C) — mirrors the existing Requester style-guard extractor. Runtime-injected
    // modifiers (@TierBadgeClass) appear as expressions, not literal tokens, so they are not scanned here.
    private static HashSet<string> UsedSdClasses(string markup)
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

    [Fact]
    public void GivenDesignSystemStylesheet_WhenChecked_ThenSdMutedIsDefined()
    {
        // Arrange
        var css = GlobalCss;

        // Act
        var defined = Defined(css, "sd-muted");

        // Assert
        Assert.True(defined, "design-system.css must define .sd-muted (the shared subdued-text token).");
        Assert.Contains("color: #5A5F6E", css);
    }

    [Fact]
    public void GivenDesignSystemStylesheet_WhenChecked_ThenSdCardBaseIsDefined()
    {
        // Arrange
        var css = GlobalCss;

        // Act
        var defined = Defined(css, "sd-card");

        // Assert — the 3-property intersection base only; padding/margin/border stay page-scoped.
        Assert.True(defined, "design-system.css must define the .sd-card base.");
        Assert.Contains("background: #fff", css);
        Assert.Contains("border-radius: 16px", css);
        Assert.Contains("box-shadow: 0 6px 20px rgba(27, 29, 41, .08)", css);
    }

    [Fact]
    public void GivenDesignSystemStylesheet_WhenChecked_ThenSdCardBodyIsDefined()
    {
        // Arrange
        var css = GlobalCss;

        // Act
        var defined = Defined(css, "sd-card__body");

        // Assert
        Assert.True(defined, "design-system.css must define .sd-card__body.");
        Assert.Contains("margin: 2px 0 12px", css);
    }

    [Fact]
    public void GivenDesignSystemStylesheet_WhenChecked_ThenSdBadgeAndAllTierModifiersAreDefined()
    {
        // Arrange
        var css = GlobalCss;

        // Act & Assert — base pill + icon + all three tier modifiers (the badge must be correct for every
        // requester tier, never a hardcoded GOLD — the BUG-034 masking guard, preserved globally here).
        Assert.True(Defined(css, "sd-badge"), "design-system.css must define the .sd-badge base.");
        Assert.Contains("border-radius: 999px", css);
        Assert.Contains("white-space: nowrap", css);
        Assert.True(Defined(css, "sd-badge__icon"), "design-system.css must define .sd-badge__icon.");
        Assert.True(Defined(css, "sd-badge--gold"), "design-system.css must define .sd-badge--gold.");
        Assert.Contains("#D4A017", css);
        Assert.True(Defined(css, "sd-badge--silver"), "design-system.css must define .sd-badge--silver.");
        Assert.Contains("#8893A0", css);
        Assert.True(Defined(css, "sd-badge--bronze"), "design-system.css must define .sd-badge--bronze.");
        Assert.Contains("#B0703C", css);
    }

    [Fact]
    public void GivenDesignSystemStylesheet_WhenChecked_ThenSdBtnBaseAndAllVariantsAreDefined()
    {
        // Arrange
        var css = GlobalCss;

        // Act & Assert — base + every variant used across the pages.
        Assert.True(Defined(css, "sd-btn"), "design-system.css must define the .sd-btn base.");
        Assert.Contains("transition: filter .12s", css);
        Assert.Contains("text-transform: none", css); // prevents MudBlazor's auto-uppercase (from SubmitRequest)
        Assert.True(Defined(css, "sd-btn--primary"), "design-system.css must define .sd-btn--primary.");
        Assert.True(Defined(css, "sd-btn--success"), "design-system.css must define .sd-btn--success.");
        Assert.True(Defined(css, "sd-btn--danger"), "design-system.css must define .sd-btn--danger.");
        Assert.True(Defined(css, "sd-btn--ghost"), "design-system.css must define .sd-btn--ghost.");
        Assert.True(Defined(css, "sd-btn--outline"), "design-system.css must define .sd-btn--outline.");
        Assert.True(Defined(css, "sd-btn--block"), "design-system.css must define .sd-btn--block.");
        Assert.Contains("width: 100%", css); // block = width only; ActiveJob's margin-top stays scoped
        Assert.True(Defined(css, "sd-btn--lg"), "design-system.css must define .sd-btn--lg.");
        Assert.True(Defined(css, "sd-btn:disabled"), "design-system.css must define .sd-btn:disabled.");

        // QUAL-011 checkpoint #1: the sd-btn--primary shadow conflict (.06/.08 in ActiveJob vs .10/.12 in
        // SubmitRequest) is resolved to the .06/.08 elevation system used by sd-map-panel / sd-eta.
        // SubmitRequest's stronger .10/.12 shadow is dropped; there is no scoped override restoring it.
        Assert.Contains("box-shadow: 0 1px 2px rgba(20, 22, 40, .06), 0 1px 3px rgba(20, 22, 40, .08)", css);
        Assert.DoesNotContain("rgba(20, 22, 40, .10)", css);
        Assert.DoesNotContain("rgba(20, 22, 40, .12)", css);
    }

    [Fact]
    public void GivenDesignSystemStylesheet_WhenChecked_ThenSdBannerAndIconAreDefined()
    {
        // Arrange
        var css = GlobalCss;

        // Act & Assert
        Assert.True(Defined(css, "sd-banner"), "design-system.css must define .sd-banner (inline error band).");
        Assert.True(Defined(css, "sd-banner__icon"), "design-system.css must define .sd-banner__icon.");
        Assert.Contains("#B5281F", css); // the error-band text colour
    }

    [Fact]
    public void GivenDesignSystemStylesheet_WhenChecked_ThenSdFieldLabelAndSelectAreDefined()
    {
        // Arrange
        var css = GlobalCss;

        // Act & Assert
        Assert.True(Defined(css, "sd-field"), "design-system.css must define .sd-field.");
        Assert.True(Defined(css, "sd-field__label"), "design-system.css must define .sd-field__label.");
        Assert.True(Defined(css, "sd-select"), "design-system.css must define .sd-select.");
        Assert.Matches(@"\.sd-select:focus", css); // the focus ring must survive the move
    }

    [Fact]
    public void GivenJobOfferMarkup_WhenEverySdClassIsChecked_ThenEachIsDefinedInGlobalOrScopedStylesheet()
    {
        // Arrange
        var scoped = Read(ServiceRepPagesDir, "JobOffer.razor.css");
        var used = UsedSdClasses(Read(ServiceRepPagesDir, "JobOffer.razor"));

        // Act
        var unresolved = used.Where(cls => !ResolvesFrom(scoped, cls)).ToList();

        // Assert
        Assert.True(
            unresolved.Count == 0,
            "JobOffer.razor uses sd-* class(es) defined neither in the global design-system.css nor in "
            + $"JobOffer.razor.css: {string.Join(", ", unresolved)}");
    }

    [Fact]
    public void GivenActiveJobMarkup_WhenEverySdClassIsChecked_ThenEachIsDefinedInGlobalOrScopedStylesheet()
    {
        // Arrange
        var scoped = Read(ServiceRepPagesDir, "ActiveJob.razor.css");
        var used = UsedSdClasses(Read(ServiceRepPagesDir, "ActiveJob.razor"));

        // Act — sd-eta__num / sd-eta__lbl are excluded: they are used in ActiveJob's markup but were never
        // defined in ActiveJob's own scoped CSS before QUAL-011 (verified against the pre-refactor
        // baseline), and they are NOT part of the shared token set moved to the global sheet — ActiveJob's
        // sd-eta box is absolutely-positioned and page-specific, so its __num/__lbl children have always
        // rendered with browser defaults on this page. QUAL-011 is a no-visual-change consolidation, so
        // newly defining them here would itself be a visual change; they are out of scope for this guard.
        var unresolved = used
            .Where(cls => !ActiveJobPreExistingUndefined.Contains(cls))
            .Where(cls => !ResolvesFrom(scoped, cls))
            .ToList();

        // Assert
        Assert.True(
            unresolved.Count == 0,
            "ActiveJob.razor uses sd-* class(es) defined neither in the global design-system.css nor in "
            + $"ActiveJob.razor.css: {string.Join(", ", unresolved)}");
    }

    [Fact]
    public void GivenRepIdleMarkup_WhenEverySdClassIsChecked_ThenEachIsDefinedInGlobalOrScopedStylesheet()
    {
        // Arrange
        var scoped = Read(ServiceRepPagesDir, "RepIdle.razor.css");
        var used = UsedSdClasses(Read(ServiceRepPagesDir, "RepIdle.razor"));

        // Act — the base sd-chip is excluded: RepIdle's markup lists it (class="sd-chip sd-chip--available")
        // but RepIdle's own scoped CSS never defined a base .sd-chip before QUAL-011 (only the fully
        // self-contained .sd-chip--available), and sd-chip is NOT a shared token moved to the global sheet
        // (ActiveJob's .sd-chip is absolutely-positioned and page-specific). The available pill has always
        // been styled entirely by .sd-chip--available. Defining a base .sd-chip now would change visuals,
        // so it is out of scope for this no-visual-change guard.
        var unresolved = used
            .Where(cls => !RepIdlePreExistingUndefined.Contains(cls))
            .Where(cls => !ResolvesFrom(scoped, cls))
            .ToList();

        // Assert
        Assert.True(
            unresolved.Count == 0,
            "RepIdle.razor uses sd-* class(es) defined neither in the global design-system.css nor in "
            + $"RepIdle.razor.css: {string.Join(", ", unresolved)}");
    }

    // Pre-existing (pre-QUAL-011) undefined-on-this-page tokens — see the two tests above for the full
    // rationale. Kept as named sets so the exclusion is explicit and auditable, never a silent skip.
    private static readonly HashSet<string> ActiveJobPreExistingUndefined = new() { "sd-eta__num", "sd-eta__lbl" };
    private static readonly HashSet<string> RepIdlePreExistingUndefined = new() { "sd-chip" };

    // AC-3: distinctive base-only properties of the shared tokens. Their presence in a scoped sheet means
    // the shared token was NOT stripped; page-specific overrides (sd-card padding, sd-btn--block margin-top)
    // never redefine these, so absence is an unambiguous "the base moved to the global sheet" signal.
    private const string BadgeBaseProperty = "border-radius: 999px";
    private const string BtnBaseProperty = "transition: filter .12s";

    [Fact]
    public void GivenJobOfferScopedCss_WhenChecked_ThenSharedBadgeAndBtnTokenBasePropertiesAreAbsent()
    {
        // Arrange
        var scoped = Read(ServiceRepPagesDir, "JobOffer.razor.css");

        // Act & Assert — the shared sd-badge and sd-btn bases now live only in design-system.css.
        Assert.DoesNotContain(BadgeBaseProperty, scoped);
        Assert.DoesNotContain(BtnBaseProperty, scoped);
    }

    [Fact]
    public void GivenActiveJobScopedCss_WhenChecked_ThenSharedBadgeTokenBasePropertiesAreAbsent()
    {
        // Arrange
        var scoped = Read(ServiceRepPagesDir, "ActiveJob.razor.css");

        // Act & Assert — ActiveJob sheds both the shared sd-badge base and the shared sd-btn base (+ primary
        // + lg + disabled); it retains only its page-specific sd-btn--block { margin-top } override, which
        // does not carry either distinctive base property.
        Assert.DoesNotContain(BadgeBaseProperty, scoped);
        Assert.DoesNotContain(BtnBaseProperty, scoped);
    }

    [Fact]
    public void GivenRepIdleScopedCss_WhenChecked_ThenSdCardIsCompletelyAbsent()
    {
        // Arrange
        var scoped = Read(ServiceRepPagesDir, "RepIdle.razor.css");

        // Act & Assert — RepIdle's claimed-vehicle card used no page-specific card additions (only the base
        // white/rounded/shadow), so it drops .sd-card entirely and relies on the global base. The #id
        // selector (#claimed-vehicle-card) is unaffected — only the .sd-card class must be gone.
        Assert.False(
            Defined(scoped, "sd-card"),
            "RepIdle.razor.css must not define .sd-card — the base now comes from the global design-system.css.");
    }

    [Fact]
    public void GivenSubmitRequestScopedCss_WhenChecked_ThenSharedTokenBasePropertiesAreAbsent()
    {
        // Arrange
        var scoped = Read(RequesterPagesDir, "SubmitRequest.razor.css");

        // Act & Assert — SubmitRequest sheds sd-muted, the sd-btn family (base + primary + outline + block +
        // lg + disabled), sd-field(+label), sd-select and sd-banner(+icon); it retains only its
        // form-specific layout (sd-submit, sd-submit__container, sd-map-panel, sd-pin-label). sd-pin-label
        // legitimately keeps color #5A5F6E, so absence is asserted via the selectors, not that colour.
        Assert.False(Defined(scoped, "sd-muted"), ".sd-muted must move to the global design-system.css.");
        Assert.False(Defined(scoped, "sd-banner"), ".sd-banner must move to the global design-system.css.");
        Assert.False(Defined(scoped, "sd-field"), ".sd-field must move to the global design-system.css.");
        Assert.False(Defined(scoped, "sd-select"), ".sd-select must move to the global design-system.css.");
        Assert.DoesNotContain(BtnBaseProperty, scoped);
    }

    [Fact]
    public void GivenRequesterPendingScopedCss_WhenChecked_ThenSharedBadgeTokenBasePropertiesAreAbsent()
    {
        // Arrange
        var scoped = Read(RequesterPagesDir, "RequesterPending.razor.css");

        // Act & Assert — RequesterPending sheds sd-muted and the sd-badge family (base + icon + tiers); it
        // retains only its page-specific sd-card override (border + padding), which carries none of the
        // badge base properties.
        Assert.False(Defined(scoped, "sd-badge"), ".sd-badge must move to the global design-system.css.");
        Assert.DoesNotContain(BadgeBaseProperty, scoped);
        Assert.DoesNotContain("white-space: nowrap", scoped); // the badge's nowrap now lives globally
        Assert.False(Defined(scoped, "sd-muted"), ".sd-muted must move to the global design-system.css.");
    }
}
