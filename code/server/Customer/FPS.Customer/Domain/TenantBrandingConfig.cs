using System.Text.RegularExpressions;

namespace FPS.Customer.Domain;

public sealed record TenantBrandingConfig
{
    private static readonly Regex HexColor =
        new(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.Compiled);

    // Keycloak identity-provider broker alias shape: starts alphanumeric, then
    // alphanumerics, dot, underscore, or hyphen. Non-secret routing data only.
    private static readonly Regex IdpAliasPattern =
        new(@"^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$", RegexOptions.Compiled);

    public string? PrimaryColor { get; init; }
    public string? AccentColor { get; init; }
    public string? LogoAssetId { get; init; }
    public string? FaviconAssetId { get; init; }
    public string? LegalFooterText { get; init; }
    public TenantLoginMode LoginMode { get; init; } = TenantLoginMode.LocalAccount;
    // Broker alias for the tenant's company SSO in the FairSpot Keycloak realm (AUTH010).
    // Exposed through anonymous tenant discovery as a routing hint (kc_idp_hint); it names
    // a broker configuration, never credentials, and never grants access by itself.
    public string? IdpAlias { get; init; }

    public static string? Validate(TenantBrandingConfig config)
    {
        if (config.PrimaryColor is not null && !HexColor.IsMatch(config.PrimaryColor))
            return "PrimaryColor must be a valid hex color (#rgb or #rrggbb).";
        if (config.AccentColor is not null && !HexColor.IsMatch(config.AccentColor))
            return "AccentColor must be a valid hex color (#rgb or #rrggbb).";
        if (config.LegalFooterText is { Length: > 500 })
            return "LegalFooterText must not exceed 500 characters.";
        if (config.IdpAlias is not null && !IdpAliasPattern.IsMatch(config.IdpAlias))
            return "IdpAlias must be 1-64 characters: alphanumerics, dot, underscore, or hyphen, starting alphanumeric.";
        if (config.IdpAlias is not null && config.LoginMode == TenantLoginMode.LocalAccount)
            return "IdpAlias requires LoginMode CompanySso or Both.";
        return null;
    }
}
