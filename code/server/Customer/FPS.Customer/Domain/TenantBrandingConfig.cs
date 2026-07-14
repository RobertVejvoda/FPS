using System.Text.RegularExpressions;

namespace FPS.Customer.Domain;

public sealed record TenantBrandingConfig
{
    private static readonly Regex HexColor =
        new(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.Compiled);

    public string? PrimaryColor { get; init; }
    public string? AccentColor { get; init; }
    public string? LogoAssetId { get; init; }
    public string? FaviconAssetId { get; init; }
    public string? LegalFooterText { get; init; }
    public TenantLoginMode LoginMode { get; init; } = TenantLoginMode.LocalAccount;

    public static string? Validate(TenantBrandingConfig config)
    {
        if (config.PrimaryColor is not null && !HexColor.IsMatch(config.PrimaryColor))
            return "PrimaryColor must be a valid hex color (#rgb or #rrggbb).";
        if (config.AccentColor is not null && !HexColor.IsMatch(config.AccentColor))
            return "AccentColor must be a valid hex color (#rgb or #rrggbb).";
        if (config.LegalFooterText is { Length: > 500 })
            return "LegalFooterText must not exceed 500 characters.";
        return null;
    }
}
