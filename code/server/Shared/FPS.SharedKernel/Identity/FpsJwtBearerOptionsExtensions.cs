using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FPS.SharedKernel.Identity;

public static class FpsJwtBearerOptionsExtensions
{
    public static void ConfigureFpsJwtBearer(
        this JwtBearerOptions options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var primaryAudience = configuration["Auth:Audience"];
        var audiences = ReadAudiences(configuration, primaryAudience);

        options.Authority = configuration["Auth:Authority"];
        options.Audience = primaryAudience;
        options.RequireHttpsMetadata = !environment.IsDevelopment()
            && !IsTruthy(configuration["Auth:AllowHttpMetadata"]);
        options.TokenValidationParameters.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
        options.TokenValidationParameters.NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
        if (audiences.Count > 0)
        {
            options.TokenValidationParameters.ValidAudiences = audiences;
        }
        if (environment.IsDevelopment() && IsTruthy(configuration["Auth:AllowLocalIssuerHostOverride"]))
        {
            var authority = options.Authority;
            options.TokenValidationParameters.IssuerValidator = (issuer, token, parameters) =>
            {
                if (IsSameRealmIssuer(authority, issuer))
                {
                    return issuer;
                }

                return Validators.ValidateIssuer(issuer, token, parameters);
            };
        }

        ConfigurePlatformIssuer(options, configuration, environment);
    }

    // PLAT001 (Target B): when a platform realm is configured (Auth:PlatformAuthority),
    // accept tokens from BOTH the customer realm (Auth:Authority) and the platform realm.
    // Signing keys are resolved from whichever realm minted the token, and both realm
    // issuers are valid. TenantClaimsTransformation then confines platform_* roles to the
    // platform issuer (Auth:PlatformIssuer == the platform realm URL). When
    // Auth:PlatformAuthority is unset the platform plane is dormant and this is a no-op —
    // single-issuer behaviour is unchanged.
    private static void ConfigurePlatformIssuer(
        JwtBearerOptions options, IConfiguration configuration, IHostEnvironment environment)
    {
        var platformAuthority = configuration["Auth:PlatformAuthority"];
        if (string.IsNullOrWhiteSpace(platformAuthority)) return;

        var requireHttps = !environment.IsDevelopment() && !IsTruthy(configuration["Auth:AllowHttpMetadata"]);

        var authorities = new[] { options.Authority, platformAuthority }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!.TrimEnd('/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Bind each issuer to its own realm's metadata so signing keys are resolved from the
        // realm that the token CLAIMS minted it — never the union of all realms' keys.
        var managersByIssuer = authorities.ToDictionary(
            a => a,
            a => new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{a}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = requireHttps }),
            StringComparer.Ordinal);

        // Mirror the dev-only same-realm host override (Auth:AllowLocalIssuerHostOverride) so a
        // token from the configured customer realm reached on a different host still resolves
        // that realm's keys (see AliasLocalDevIssuer). Cross-realm issuers are never aliased.
        var allowLocalOverride = environment.IsDevelopment()
            && IsTruthy(configuration["Auth:AllowLocalIssuerHostOverride"]);
        var customerAuthority = options.Authority?.TrimEnd('/');

        var tvp = options.TokenValidationParameters;
        tvp.ValidateIssuer = true;
        tvp.ValidIssuers = authorities;
        // PLAT001 hardening: resolve signing keys ONLY from the realm named by the token's
        // `iss`, not the union of all realms. Pooling keys across realms would let a token
        // minted by one realm (e.g. the customer realm) validate while claiming another
        // realm's issuer (the platform issuer) — TenantClaimsTransformation trusts `iss` to
        // grant platform_* roles, so cross-realm key pooling is a tenant→platform escalation
        // path. With keys bound to the issuer, a cross-realm-signed token fails signature
        // validation. A temporarily unreachable realm only fails its own tokens.
        tvp.IssuerSigningKeyResolver = (_, securityToken, kid, _) =>
            ResolveKeysForIssuer(
                AliasLocalDevIssuer(securityToken?.Issuer, customerAuthority, allowLocalOverride, authorities),
                kid, SnapshotSigningKeys(managersByIssuer));
    }

    // Dev-only: when Auth:AllowLocalIssuerHostOverride is enabled, a token from the SAME Keycloak
    // realm reached on a different host (e.g. a LAN IP instead of localhost) is mapped back to the
    // configured customer authority so its keys resolve — mirroring the issuer-validator override.
    // Cross-realm issuers are NEVER aliased, so the issuer→key binding (the security fix) holds:
    // a token claiming a different realm still resolves only that realm's keys (or none).
    internal static string? AliasLocalDevIssuer(
        string? tokenIssuer, string? customerAuthority, bool allowLocalOverride,
        IReadOnlyCollection<string> configuredAuthorities)
    {
        if (string.IsNullOrEmpty(tokenIssuer)) return tokenIssuer;
        var issuer = tokenIssuer.TrimEnd('/');
        if (!allowLocalOverride || string.IsNullOrEmpty(customerAuthority)) return issuer;
        if (configuredAuthorities.Contains(issuer, StringComparer.Ordinal)) return issuer; // already configured
        return IsSameRealmIssuer(customerAuthority, tokenIssuer) ? customerAuthority : issuer;
    }

    // Snapshots each realm's current signing keys keyed by issuer. A realm whose metadata is
    // unreachable is omitted, so its tokens fail closed while other realms still validate.
    private static IReadOnlyDictionary<string, IReadOnlyCollection<SecurityKey>> SnapshotSigningKeys(
        IReadOnlyDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> managersByIssuer)
    {
        var snapshot = new Dictionary<string, IReadOnlyCollection<SecurityKey>>(StringComparer.Ordinal);
        foreach (var (issuer, manager) in managersByIssuer)
        {
            try
            {
                snapshot[issuer] = manager.GetConfigurationAsync(CancellationToken.None)
                    .GetAwaiter().GetResult().SigningKeys.ToList();
            }
            catch
            {
                // realm metadata unreachable right now — omit; its tokens fail closed
            }
        }
        return snapshot;
    }

    // PLAT001 hardening (security-critical): return signing keys ONLY for the realm named by
    // the token's `iss`. This binds the signature to the claimed issuer, so a token minted by
    // one realm cannot validate while claiming another realm's issuer (a customer→platform
    // escalation path if keys were pooled). Unknown issuer → no keys → token rejected.
    internal static IList<SecurityKey> ResolveKeysForIssuer(
        string? tokenIssuer, string? kid,
        IReadOnlyDictionary<string, IReadOnlyCollection<SecurityKey>> keysByIssuer)
    {
        var issuer = tokenIssuer?.TrimEnd('/');
        if (issuer is null || !keysByIssuer.TryGetValue(issuer, out var keys))
            return [];
        return FilterByKid(keys, kid);
    }

    // Returns the keys whose `kid` matches when any do (the common JWKS case), otherwise all
    // of the realm's keys (covers rotation where a token's kid may not be individually
    // matchable yet). The caller has already bound `keys` to the token's issuer.
    private static IList<SecurityKey> FilterByKid(IReadOnlyCollection<SecurityKey> keys, string? kid)
    {
        if (string.IsNullOrEmpty(kid)) return keys.ToList();
        var matched = keys.Where(k => string.Equals(k.KeyId, kid, StringComparison.Ordinal)).ToList();
        return matched.Count > 0 ? matched : keys.ToList();
    }

    private static IReadOnlyList<string> ReadAudiences(IConfiguration configuration, string? primaryAudience)
    {
        var audiences = new List<string>();
        AddAudience(audiences, primaryAudience);

        var additional = configuration["Auth:AdditionalAudiences"];
        if (!string.IsNullOrWhiteSpace(additional))
        {
            foreach (var audience in additional.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddAudience(audiences, audience);
            }
        }

        foreach (var child in configuration.GetSection("Auth:AdditionalAudiences").GetChildren())
        {
            AddAudience(audiences, child.Value);
        }

        return audiences;
    }

    private static void AddAudience(List<string> audiences, string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience)) return;
        if (!audiences.Contains(audience, StringComparer.Ordinal))
        {
            audiences.Add(audience);
        }
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameRealmIssuer(string? authority, string? issuer)
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)) return false;
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)) return false;
        return string.Equals(authorityUri.AbsolutePath.TrimEnd('/'), issuerUri.AbsolutePath.TrimEnd('/'), StringComparison.Ordinal)
            && authorityUri.AbsolutePath.Contains("/realms/", StringComparison.Ordinal);
    }
}
