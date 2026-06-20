using MudBlazor;

namespace ServiceDelivery.Client.UI.Theme;

/// <summary>
/// Single source of truth for the application's MudBlazor theme tokens.
/// Centralising the primary indigo-violet brand colour and the
/// sentence-case button transform here means the colour and the
/// "Sign in" (not "SIGN IN") fix apply app-wide rather than being
/// re-stated per component.
/// </summary>
public static class AppTheme
{
    private const string PrimaryIndigo = "#5B4FE0";

    public static MudTheme Instance { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = PrimaryIndigo,
            AppbarBackground = PrimaryIndigo,
        },
        Typography = new Typography
        {
            Button = new ButtonTypography
            {
                TextTransform = "none",
            },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
        },
    };
}
